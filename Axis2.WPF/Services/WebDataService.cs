using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using Axis2.WPF.Models;

namespace Axis2.WPF.Services
{
    /// <summary>
    /// Fetches item / NPC data from an Axis Sphere51 Data Server (Web Profile),
    /// so the tool can work without any local scripts. Maps the server's JSON
    /// onto the same <see cref="SObject"/> model the local parser produces.
    /// </summary>
    public static class WebDataService
    {
        private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(60) };
        private static readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };

        private sealed class WebObj
        {
            public string Id { get; set; } = "";
            public string Type { get; set; } = "item";
            public string File { get; set; } = "";
            public string Description { get; set; } = "";
            public string Category { get; set; } = "";
            public string Subsection { get; set; } = "";
            public string DisplayId { get; set; } = "";
            public string? Color { get; set; }
            public string? ScriptType { get; set; }
        }

        private sealed class Paged
        {
            public int Total { get; set; }
            public List<WebObj> Items { get; set; } = new();
        }

        private sealed class WebRect
        {
            public int X { get; set; }
            public int Y { get; set; }
            public int W { get; set; }
            public int H { get; set; }
        }

        private sealed class WebRegion
        {
            public string Kind { get; set; } = "area";
            public string Name { get; set; } = "";
            public string Group { get; set; } = "";
            public string DefName { get; set; } = "";
            public int Map { get; set; }
            public int X { get; set; }
            public int Y { get; set; }
            public int Z { get; set; }
            public List<WebRect> Rects { get; set; } = new();
            public List<WebRegion> Rooms { get; set; } = new();
        }

        private sealed class WebSpell
        {
            public int Id { get; set; }
            public string DefName { get; set; } = "";
            public string Name { get; set; } = "";
            public string Resources { get; set; } = "";
        }

        private static System.Net.Http.Headers.AuthenticationHeaderValue? BasicAuth(string? user, string? password)
        {
            if (string.IsNullOrEmpty(user))
                return null;
            var token = System.Convert.ToBase64String(
                System.Text.Encoding.UTF8.GetBytes($"{user}:{password}"));
            return new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", token);
        }

        private static async Task<HttpResponseMessage> GetAsync(string url, string? user, string? password)
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = BasicAuth(user, password);
            var res = await _http.SendAsync(req).ConfigureAwait(false);
            if (res.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                throw new WebAuthException("Unauthorized — check the login and password.");
            if (res.StatusCode == System.Net.HttpStatusCode.Forbidden)
                throw new WebAuthException("Access denied — your account PLEVEL is below the required level.");
            res.EnsureSuccessStatusCode();
            return res;
        }

        /// <summary>kind = "items" or "npcs". Pages through the whole collection.</summary>
        public static async Task<List<SObject>> FetchAsync(string baseUrl, string kind, string? user = null, string? password = null)
        {
            var result = new List<SObject>();
            var root = baseUrl.TrimEnd('/');
            int offset = 0;
            const int limit = 2000;

            while (true)
            {
                var url = $"{root}/api/{kind}?offset={offset}&limit={limit}";
                using var res = await GetAsync(url, user, password).ConfigureAwait(false);
                var page = await res.Content.ReadFromJsonAsync<Paged>(_json).ConfigureAwait(false);
                if (page?.Items == null || page.Items.Count == 0)
                    break;

                result.AddRange(page.Items.Select(Map));
                offset += page.Items.Count;
                if (offset >= page.Total)
                    break;
            }
            return result;
        }

        /// <summary>Returns (items, npcs) from /api/stats; throws if unreachable or unauthorised.</summary>
        public static async Task<(int items, int npcs)> StatsAsync(string baseUrl, string? user = null, string? password = null)
        {
            var root = baseUrl.TrimEnd('/');
            using var res = await GetAsync($"{root}/api/stats", user, password).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync().ConfigureAwait(false));
            var r = doc.RootElement;
            return (r.GetProperty("items").GetInt32(), r.GetProperty("npcs").GetInt32());
        }

        /// <summary>
        /// Fetches Travel regions (areas with nested rooms + standalone rooms) and maps them onto
        /// the same <see cref="SObject"/>/<see cref="MapRegion"/> shape the local ScriptParser
        /// produces, so the Travel tab builds its tree identically from local scripts or the server.
        /// </summary>
        public static async Task<List<SObject>> FetchRegionsAsync(string baseUrl, string? user = null, string? password = null)
        {
            var root = baseUrl.TrimEnd('/');
            using var res = await GetAsync($"{root}/api/regions", user, password).ConfigureAwait(false);
            var regions = await res.Content.ReadFromJsonAsync<List<WebRegion>>(_json).ConfigureAwait(false)
                          ?? new List<WebRegion>();
            return regions.Select(MapRegion).ToList();
        }

        /// <summary>Fetches parsed [SPELL] blocks for the Misc tab.</summary>
        public static async Task<List<Spell>> FetchSpellsAsync(string baseUrl, string? user = null, string? password = null)
        {
            var root = baseUrl.TrimEnd('/');
            using var res = await GetAsync($"{root}/api/spells", user, password).ConfigureAwait(false);
            var spells = await res.Content.ReadFromJsonAsync<List<WebSpell>>(_json).ConfigureAwait(false)
                         ?? new List<WebSpell>();
            return spells.Select(s => new Spell
            {
                ID = s.Id,
                DefName = s.DefName ?? "",
                Name = s.Name ?? "",
                Resources = s.Resources ?? "",
            }).ToList();
        }

        private static SObject MapRegion(WebRegion r)
        {
            bool isArea = string.Equals(r.Kind, "area", StringComparison.OrdinalIgnoreCase);
            return new SObject
            {
                Id = r.Name,
                Value = r.Name,
                DisplayId = r.Name,
                Description = r.Name,
                Type = isArea ? SObjectType.Area : SObjectType.Room,
                Region = BuildRegion(r),
            };
        }

        private static MapRegion BuildRegion(WebRegion r)
        {
            if (string.Equals(r.Kind, "area", StringComparison.OrdinalIgnoreCase))
            {
                var area = new AreaDefinition
                {
                    Name = r.Name,
                    Group = r.Group ?? "",
                    Map = r.Map,
                    P = new System.Windows.Point(r.X, r.Y),
                    Z = r.Z,
                };
                foreach (var rect in r.Rects)
                    area.Rects.Add(new Rect(rect.X, rect.Y, rect.W, rect.H));
                foreach (var room in r.Rooms)
                    area.Rooms.Add((RoomDefinition)BuildRegion(room));
                return area;
            }

            var roomDef = new RoomDefinition
            {
                Name = r.Name,
                Group = r.Group ?? "",
                Map = r.Map,
                P = new System.Windows.Point(r.X, r.Y),
                Z = r.Z,
                DefName = r.DefName ?? "",
            };
            foreach (var rect in r.Rects)
                roomDef.Rects.Add(new Rect(rect.X, rect.Y, rect.W, rect.H));
            return roomDef;
        }

        private static SObject Map(WebObj o) => new()
        {
            Id = o.Id,
            Value = o.Id,
            DisplayId = string.IsNullOrEmpty(o.DisplayId) ? o.Id : o.DisplayId,
            Description = o.Description,
            Category = o.Category,
            SubSection = o.Subsection,
            Color = o.Color ?? "",
            ScriptType = o.ScriptType ?? "",
            FileName = o.File,
            Type = string.Equals(o.Type, "npc", StringComparison.OrdinalIgnoreCase)
                ? SObjectType.Npc : SObjectType.Item,
        };
    }

    /// <summary>Raised on 401/403 from the data server (bad login or insufficient PLEVEL).</summary>
    public class WebAuthException : Exception
    {
        public WebAuthException(string message) : base(message) { }
    }
}
