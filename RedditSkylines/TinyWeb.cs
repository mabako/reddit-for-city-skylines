using SimpleJson;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace RedditClient
{
    internal class TinyWeb
    {
        private const string BASE_URL = "http://www.reddit.com{0}.json?limit={1}";
        public static IEnumerable<RedditPost> FindLastPosts(string subreddit)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(string.Format(BASE_URL, subreddit, RedditUpdater.MAX_REDDIT_POSTS_PER_SUBREDDIT));
            request.Method = WebRequestMethods.Http.Get;
            request.Accept = "text/json";

            using (HttpWebResponse response = (HttpWebResponse) request.GetResponse())
            {
                if (response.StatusCode != HttpStatusCode.OK)
                    return null;

                string str = new StreamReader(response.GetResponseStream()).ReadToEnd();
                
                JsonObject root = (JsonObject)SimpleJson.SimpleJson.DeserializeObject(str);
                JsonObject rootData = (JsonObject)root["data"];
                JsonArray rootChildren = (JsonArray)rootData["children"];

                var list = new List<RedditPost>();
                foreach(object obj in rootChildren)
                {
                    JsonObject child = (JsonObject)obj;
                    JsonObject data = (JsonObject)child["data"];

                    var post = createPost(data);
                    if(post != null)
                        list.Add(post);
                }
                return list;
            }
        }

        private static RedditPost createPost(JsonObject data)
        {
            // Any karma at all?
            var karma = data["score"];
            if(karma is Int32)
                if((Int32)karma <= 0)
                    return null;


            // Sticky post?
            var sticky = data["stickied"];
            if(sticky is Boolean)
                if ((Boolean)sticky == true)
                    return null;

            // create post object
            var post = new RedditPost { id = data["id"].ToString(), title = data["title"].ToString(), author = data["author"].ToString(), subreddit = data["subreddit"].ToString() };

            // does it have a flair?
            var flair = data["link_flair_text"];
            if (flair != null)
            {
                var flairStr = flair.ToString();
                if (flairStr.Equals("meta", StringComparison.InvariantCultureIgnoreCase))
                    return null;

                post.title += " #" + flairStr.Replace(" ", "");
            }

            return post;
        }
    }

    internal class RedditPost
    {
        internal string title;
        internal string author;
        internal string id;
        internal string subreddit;
    }
}
