using ColossalFramework;
using ICities;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Timers;

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
    }

    public class RedditUpdater : ChirperExtensionBase
    {
        public const int MAX_REDDIT_POSTS_PER_SUBREDDIT = 5;
        public const int MAX_CACHED_REDDIT_POSTS_PER_SUBREDDIT = 50;

        private Timer timer = new Timer();
        private Dictionary<string, Queue<string>> lastPostIds = new Dictionary<string, Queue<string>>();

        public override void OnCreated(IChirper threading)
        {
            try
            {
                Configuration.Load();
                if (Configuration.Subreddits.Count >= 1)
                {
                    DebugOutputPanel.AddMessage(ColossalFramework.Plugins.PluginManager.MessageType.Message, string.Format("Going to show a new message from one of {0} subreddits every {1} seconds (NoSound = {2})", Configuration.Subreddits.Count, Configuration.TimerInSeconds, Configuration.NoSound));

                    foreach (string subreddit in Configuration.Subreddits)
                        lastPostIds.Add(subreddit, new Queue<string>());

                    timer.AutoReset = true;
                    timer.Elapsed += new ElapsedEventHandler((sender, e) => UpdateRedditPosts());
                    timer.Interval = Configuration.TimerInSeconds * 1000;
                    timer.Start();
                }
                else
                {
                    DebugOutputPanel.AddMessage(ColossalFramework.Plugins.PluginManager.MessageType.Message, "No subreddits configured.");
                }
            }
            catch(Exception e)
            {
                DebugOutputPanel.AddMessage(ColossalFramework.Plugins.PluginManager.MessageType.Error, string.Format("[Reddit] {0}: {1}", e.GetType(), e.Message));
            }
        }

        public override void OnReleased()
        {
            timer.Stop();
            timer.Dispose();
        }

        private void UpdateRedditPosts()
        {
            // Pick a subreddit at random
            string subreddit = Configuration.Subreddits[new Random().Next(Configuration.Subreddits.Count)];
            try
            {
                // Remove posts that are no longer checked against; plus some for possible deletions
                Queue<string> lastPostId = lastPostIds[subreddit];
                while (lastPostId.Count > MAX_CACHED_REDDIT_POSTS_PER_SUBREDDIT)
                    lastPostId.Dequeue();

                // Fetch a number of latest posts
                IEnumerable<RedditPost> newestPosts = TinyWeb.FindLastPosts(subreddit);
                foreach (RedditPost newestPost in newestPosts)
                {
                    // Find the first one we haven't shown yet
                    if (!lastPostId.Contains(newestPost.id))
                    {
                        AddMessage(new Message(newestPost.author, newestPost.subreddit, newestPost.title));
                        lastPostIds[subreddit].Enqueue(newestPost.id);
                        return;
                    }
                }
            }
            catch (Exception e)
            {
                DebugOutputPanel.AddMessage(ColossalFramework.Plugins.PluginManager.MessageType.Message, string.Format("[Reddit {0}] {1}: {2}", subreddit, e.GetType().ToString(), e.Message)); 
            }
        }

        private void AddMessage(Message m)
        {
            if (Configuration.NoSound >= 1)
            {
                // This behaviour breaks custom chirper panels, like the marquee overlay. Don't use it if you need custom chirps.
                var panel = Singleton<ChirpPanel>.instance;

                if (Configuration.NoSound >= 2)
                {
                    if (!panel.isShowing)
                        panel.Expand(10f);
                }
                panel.AddEntry(m, true);
            }
            else
            {
                Singleton<MessageManager>.instance.QueueMessage(m);
            }
        }
    }
    public class Message : MessageBase
    {
        private string m_author;
        private string m_subreddit;
        private string m_text;

        public Message(string author, string subreddit, string text)
        {
            m_author = author;
            m_subreddit = subreddit;
            m_text = text;
        }

        public override uint GetSenderID()
        {
            return 0;
        }

        public override string GetSenderName()
        {
            return m_author;
        }

        public override string GetText()
        {
            return string.Format("{0} #{1}", m_text, m_subreddit);
        }

        /// <summary>
        /// We basically want to ensure the same messages aren't shown twice.
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public override bool IsSimilarMessage(MessageBase other)
        {
            var m = other as Message;
            return m != null && ((m.m_author == m_author && m.m_subreddit == m_subreddit) || m.m_text == m_text);
        }

        public override void Serialize(ColossalFramework.IO.DataSerializer s)
        {
            s.WriteSharedString(m_author);
            s.WriteSharedString(m_subreddit);
            s.WriteSharedString(m_text);
        }

        public override void Deserialize(ColossalFramework.IO.DataSerializer s)
        {
            m_author = s.ReadSharedString();
            m_subreddit = s.ReadSharedString();
            m_text = s.ReadSharedString();
        }

        public override void AfterDeserialize(ColossalFramework.IO.DataSerializer s)
        {
        }
    }
}
