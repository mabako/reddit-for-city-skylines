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
        private const string SHOWERTHOUGHTS = "http://www.reddit.com/r/{0}/new.json?limit=1";
        public static RedditPost FindLastPost(string subreddit)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(string.Format(SHOWERTHOUGHTS, subreddit));
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
                JsonObject firstChild = (JsonObject)rootChildren[0];
                JsonObject first = (JsonObject)firstChild["data"];

                return new RedditPost { id = first["id"].ToString(), title = first["title"].ToString(), author = first["author"].ToString() };
            }
        }
    }

    internal class RedditPost
    {
        internal string title;
        internal string author;
        internal string id;
    }
}
