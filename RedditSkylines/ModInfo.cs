﻿using ICities;

namespace RedditClient
{
    public class ModInfo : IUserMod
    {
        public string Description
        {
            get { return "Show what's new on Reddit"; }
        }

        public string Name
        {
            get { return "Reddit for Chirpy"; }
        }

        public static int Version
        {
            get { return 9; }
        }
    }
}
