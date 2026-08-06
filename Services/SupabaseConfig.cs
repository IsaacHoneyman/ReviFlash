using System;

namespace ReviFlash.Services
{
    public static class SupabaseConfig
    {
        public static string? ProjectUrl { get; } = "https://hegjwggsueldwtnxpnnv.supabase.co";
        public static string? AnonKey { get; } = "sb_publishable_XETpGgIYnJHFwrq28EV-4w_-aZ_UdV4";
        public static string? CurrentAccessToken { get; set; }
        public static string? CurrentUserId { get; set; }
        public static string? CurrentUsername { get; set; }
    }
}
