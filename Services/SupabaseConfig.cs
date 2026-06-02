using System;

namespace ReviFlash.Services
{
    public static class SupabaseConfig
    {
        // Default values -- replace with your project values or set environment variables SUPABASE_URL and SUPABASE_ANON_KEY
        public static string? ProjectUrl { get; } =
            "https://hegjwggsueldwtnxpnnv.supabase.co";

        public static string? AnonKey { get; } =
            "sb_publishable_XETpGgIYnJHFwrq28EV-4w_-aZ_UdV4";
    }
}
