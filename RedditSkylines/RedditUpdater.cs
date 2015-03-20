using ColossalFramework;
using ColossalFramework.UI;
using ICities;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Timers;
using UnityEngine;

namespace RedditClient
{
    public class RedditUpdater : ChirperExtensionBase
    {
        public const int MAX_REDDIT_POSTS_PER_SUBREDDIT = 5;
        public const int MAX_CACHED_REDDIT_POSTS_PER_SUBREDDIT = 50;

        private System.Timers.Timer timer = new System.Timers.Timer();
        private Dictionary<string, Queue<string>> lastPostIds = new Dictionary<string, Queue<string>>();

        private AudioClip messageSound = null;
        private bool checkedAnnouncement = false;

        private CitizenMessage lastCitizenMessage = null;

        private bool IsPaused
        {
            get
            {
                return SimulationManager.instance.SimulationPaused;
            }
        }

        public override void OnCreated(IChirper threading)
        {
            try
            {
                messageSound = Singleton<ChirpPanel>.instance.m_NotificationSound;

                Configuration.Load();
                if (Configuration.Subreddits.Count >= 1)
                {
                    DebugOutputPanel.AddMessage(ColossalFramework.Plugins.PluginManager.MessageType.Message, string.Format("Going to show a new message from one of {0} subreddits every {1} seconds (AssociationMode = {2})", Configuration.Subreddits.Count, Configuration.TimerInSeconds, Configuration.AssociationMode));

                    foreach (string subreddit in Configuration.Subreddits)
                    {
                        if(!lastPostIds.ContainsKey(subreddit))
                            lastPostIds.Add(subreddit, new Queue<string>());
                    }

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

            ChirpPanel cp = ChirpPanel.instance;
            if(cp != null)
                cp.m_NotificationSound = messageSound;
        }

        private void UpdateRedditPosts()
        {
            DebugOutputPanel.AddMessage(ColossalFramework.Plugins.PluginManager.MessageType.Message, string.Format("DoUpdate {0}", IsPaused));
            if (IsPaused)
                return;

            // Possibly important messages
            if (CheckAnnouncement())
                return;

            // Pick a subreddit at random
            string subreddit = Configuration.Subreddits[new System.Random().Next(Configuration.Subreddits.Count)];

            DebugOutputPanel.AddMessage(ColossalFramework.Plugins.PluginManager.MessageType.Message, string.Format("sr {0}", subreddit));
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
                        DebugOutputPanel.AddMessage(ColossalFramework.Plugins.PluginManager.MessageType.Message, "Finding Person...");
                        var data = LookupOrRenameCitizenID(newestPost.author);

                        DebugOutputPanel.AddMessage(ColossalFramework.Plugins.PluginManager.MessageType.Message, string.Format("Person {0} {1}", data.ID, data.Name));
                        
                        AddMessage(new Message(data.Name, newestPost.subreddit, newestPost.title, data.ID));
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

        private CitizenInfo LookupOrRenameCitizenID(string name)
        {
            if (!string.IsNullOrEmpty(name))
            {
                if (Configuration.AssociationMode == 1)
                {
                    // Use any citizen's name. Not necessarily consistent in and of itself, in that the same person may show up as multiple actual CIMs.
                    uint id = MessageManager.instance.GetRandomResidentID();
                    if (id != 0u)
                        return new CitizenInfo { ID = id, Name = CitizenManager.instance.GetCitizenName(id) };
                }
                else if (Configuration.AssociationMode == 2)
                {
                    // Overwrite any CIM's name by their reddit username.
                    // To be fair: this was the more interesting part.
                    try
                    {
                        // use the shared lock for this
                        object L = GetPrivateVariable<object>(InstanceManager.instance, "m_lock");
                        do { }
                        while (!Monitor.TryEnter(L, SimulationManager.SYNCHRONIZE_TIMEOUT));
                        try
                        {
                            // do we have someone called <name>?
                            var dict = GetPrivateVariable<Dictionary<InstanceID, string>>(InstanceManager.instance, "m_names");

                            foreach (var entry in dict)
                            {
                                if (name == entry.Value)
                                    return new CitizenInfo { ID = entry.Key.Citizen, Name = name };
                            }
                        }
                        finally
                        {
                            Monitor.Exit(L);
                        }

                        for (int i = 0; i < 500; ++i)
                        {
                            uint id = MessageManager.instance.GetRandomResidentID();
                            // What probably happens when we have no residents
                            if (id == 0u)
                                break;

                            // doesn't exist
                            if (CitizenManager.instance.m_citizens.m_buffer[id].m_flags == Citizen.Flags.None)
                                continue;

                            // has a name
                            if ((CitizenManager.instance.m_citizens.m_buffer[id].m_flags & Citizen.Flags.CustomName) != Citizen.Flags.None)
                                continue;

                            // Found a random citizen without a name
                            CitizenManager.instance.StartCoroutine(CitizenManager.instance.SetCitizenName(id, name));
                            return new CitizenInfo { ID = id, Name = name };
                        }
                        // either we've tried 500 CIMs which all were named (bad luck or too few people), or
                        // we have no people at all.
                    }
                    catch
                    {
                        // not sure if this would happen often. Who knows.
                        DebugOutputPanel.AddMessage(ColossalFramework.Plugins.PluginManager.MessageType.Message, string.Format("[Reddit] Failed to pick random citizen name for {0}", name));
                    }
                }
            }

            // Either we have no people, or we have some people but couldn't find anyone to use for our purposes,
            // or we don't want people renamed.
            return new CitizenInfo { Name = name };
        }

        private bool CheckAnnouncement()
        {
            if (checkedAnnouncement)
                return false;

            checkedAnnouncement = true;

            try
            {
                string announcement = TinyWeb.GetAnnouncement();
                if (announcement != null && announcement.Length > 2)
                {
                    announcement = announcement.Trim();

                    if (Configuration.LastAnnouncement == announcement.GetHashCode())
                        return false;

                    Configuration.LastAnnouncement = announcement.GetHashCode();
                    Configuration.SaveConfig(false);

                    AddMessage(new Message("Reddit for Chirpy", "Update", announcement, 0));
                    return true;
                }
            }
            catch
            {
            }
            return false;
        }

        private void AddMessage(Message m)
        {
            if (IsPaused)
                return;

            MessageManager.instance.QueueMessage(m);
        }

        public override void OnNewMessage(IChirperMessage message)
        {
            CitizenMessage cm = message as CitizenMessage;
            if (cm != null)
            {
#if false
                DebugOutputPanel.AddMessage(ColossalFramework.Plugins.PluginManager.MessageType.Message, string.Format("Reddit | CitizenMessage {0} {1}", cm.m_messageID, cm.m_keyID));
#endif
                if (ShouldFilter(cm.m_messageID))
                {
                    ChirpPanel.instance.m_NotificationSound = null;
                    lastCitizenMessage = cm;
                }
            }
        }

        private bool ShouldFilter(string p)
        {
            if (Configuration.FilterMessages >= 2)
                return true;
            if (Configuration.FilterMessages == 0)
                return false;

            switch(p)
            {
                case LocaleID.CHIRP_ASSISTIVE_TECHNOLOGIES:
                case LocaleID.CHIRP_ATTRACTIVE_CITY:
                case LocaleID.CHIRP_CHEAP_FLOWERS:
                case LocaleID.CHIRP_DAYCARE_SERVICE:
                case LocaleID.CHIRP_HAPPY_PEOPLE:
                case LocaleID.CHIRP_HIGH_TECH_LEVEL:
                case LocaleID.CHIRP_LOW_CRIME:
                case LocaleID.CHIRP_NEW_FIRE_STATION:
                case LocaleID.CHIRP_NEW_HOSPITAL:
                case LocaleID.CHIRP_NEW_MAP_TILE:
                case LocaleID.CHIRP_NEW_MONUMENT:
                case LocaleID.CHIRP_NEW_PARK:
                case LocaleID.CHIRP_NEW_PLAZA:
                case LocaleID.CHIRP_NEW_POLICE_HQ:
                case LocaleID.CHIRP_NEW_TILE_PLACED:
                case LocaleID.CHIRP_NEW_UNIVERSITY:
                case LocaleID.CHIRP_NEW_WIND_OR_SOLAR_PLANT:
                case LocaleID.CHIRP_ORGANIC_FARMING:
                case LocaleID.CHIRP_POLICY:
                case LocaleID.CHIRP_PUBLIC_TRANSPORT_EFFICIENCY:
                case LocaleID.CHIRP_RANDOM:
                case LocaleID.CHIRP_STUDENT_LODGING:
                    return true;
                default:
                    return false;
            }
        }

        public override void OnUpdate()
        {
            if (lastCitizenMessage == null)
                return;

            // This code is roughly based on the work by Juuso "Zuppi" Hietala.
            var container = ChirpPanel.instance.transform.FindChild("Chirps").FindChild("Clipper").FindChild("Container").gameObject.transform;
            for (int i = 0; i < container.childCount; ++i)
            {
                if (container.GetChild(i).GetComponentInChildren<UILabel>().text.Equals(lastCitizenMessage.GetText()))
                {
                    ChirpPanel.instance.m_NotificationSound = messageSound;

                    UITemplateManager.RemoveInstance("ChirpTemplate", container.GetChild(i).GetComponent<UIPanel>());
                    MessageManager.instance.DeleteMessage(lastCitizenMessage);
                    lastCitizenMessage = null;

                    ChirpPanel.instance.Collapse();

                    break;
                }
            }
        }

        /// <summary>
        /// Resolve private assembly fields
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="obj"></param>
        /// <param name="fieldName"></param>
        /// <returns></returns>
        private T GetPrivateVariable<T>(object obj, string fieldName)
        {
            return (T) obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(obj);
        }
    }
}
