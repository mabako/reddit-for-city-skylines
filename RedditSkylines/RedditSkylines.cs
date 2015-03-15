using ColossalFramework;
using ICities;
using System;
using System.Text;
using System.Timers;

namespace RedditClient
{
    public class ModInfo : IUserMod
    {
        public string Description
        {
            get { return "Show what's new on /r/showerthoughts"; }
        }

        public string Name
        {
            get { return "Reddit for Chirpy"; }
        }
    }

    public class RedditUpdater : ChirperExtensionBase
    {
        private Timer timer = new Timer();
        private string lastPostId = null;

        public override void OnCreated(IChirper threading)
        {
            DebugOutputPanel.AddMessage(ColossalFramework.Plugins.PluginManager.MessageType.Message, "Loaded RedditX thing");
            timer.AutoReset = true;
            timer.Elapsed += new ElapsedEventHandler((sender, e) => UpdateRedditPosts());
            timer.Interval = 5 * 60 * 1000;
            timer.Start();
        }

        public override void OnReleased()
        {
            timer.Stop();
            timer.Dispose();
        }

        private void UpdateRedditPosts()
        {
            try
            {
                RedditPost newestPost = TinyWeb.FindLastPost("showerthoughts");
                if (newestPost.id != lastPostId)
                {
                    AddMessage(new Message { senderName = string.Format("{0}", newestPost.author), text = newestPost.title });
                    lastPostId = newestPost.id;
                }
            }
            catch (Exception e)
            {
                DebugOutputPanel.AddMessage(ColossalFramework.Plugins.PluginManager.MessageType.Message, "Failed to update reddit posts: " + e.GetType().ToString() + ": " + e.Message); 
            }
        }

        private void AddMessage(Message m)
        {
            Singleton<ChirpPanel>.instance.AddMessage(m);
        }
    }
    public class Message : IChirperMessage
    {
        public uint senderID
        {
            get { return 0; }
        }
        public string senderName { get; set; }
        public string text { get; set; }
    }
}
