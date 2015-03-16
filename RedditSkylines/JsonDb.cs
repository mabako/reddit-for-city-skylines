using ColossalFramework.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RedditClient
{
    class JsonDb
    {
        public Dictionary<string, List<Message>> Messages { get; private set; }

        private string DbPath
        {
            get
            {
                // base it on the path Cities: Skylines uses
                string path = string.Format("{0}/{1}/", DataLocation.localApplicationData, "ModConfig");
                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);

                path += "reddit-for-chirpy.db";

                return path;
            }
        }

        public JsonDb()
        {

            Load();
        }

        private void Load()
        {
            try
            {
                Messages = SimpleJson.SimpleJson.DeserializeObject<Dictionary<string, List<Message>>>(File.ReadAllText(DbPath));
            }
            catch(Exception)
            {
                Messages = new Dictionary<string, List<Message>>();
            }
        }

        internal void Save()
        {
            File.WriteAllText(DbPath, SimpleJson.SimpleJson.SerializeObject(Messages));
        }

        internal void Ensure(string p)
        {
            if(!Messages.ContainsKey(p))
            {
                Messages[p] = new List<Message>();
            }
        }

        internal Message GetRandom(string p)
        {
            return new Message("XX", "uu", string.Format("JsonDB {0}", p));
            Ensure(p);
            int count = Messages[p].Count;
            if (count == 0)
                return null;
            return Messages[p][new Random().Next(count)];
        }
    }
}
