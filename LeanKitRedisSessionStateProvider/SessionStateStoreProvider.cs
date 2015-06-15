using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Web;
using System.Web.SessionState;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LeanKit.Web.Redis
{
    public class SessionStateStoreProvider : RedisAspNetProviders.SessionStateStoreProvider
    {
        protected bool ThrowOnError { get; set; }
        protected Exception LastException { get; set; }

        public override void Initialize(string name, NameValueCollection config)
        {
            if (config["throwOnError"] != null)
            {
                bool throwOnError = false;
                bool.TryParse(config["throwOnError"], out throwOnError);
                ThrowOnError = throwOnError;
            }
            if (!string.IsNullOrEmpty(config["applicationName"]))
            {
                config["keyPrefix"] = config["applicationName"] + "_";
            }
            if (!string.IsNullOrEmpty(config["host"]) && !string.IsNullOrEmpty(config["port"]))
            {
                config["connectionString"] = config["host"] + ":" + config["port"];
            }
            if (!string.IsNullOrEmpty(config["databaseId"]))
            {
                config["dbNumber"] = config["databaseId"];
            }
            if (!string.IsNullOrEmpty(config["connectionTimeoutInMilliseconds"]))
            {
                config["connectionString"] = config["connectionString"] + ",connectTimeout=" +
                                             config["connectionTimeoutInMilliseconds"];
            }
            if (!string.IsNullOrEmpty(config["operationTimeoutInMilliseconds"]))
            {
                config["responseTimeout"] = config["operationTimeoutInMilliseconds"];
            }
            base.Initialize(name, config);
        }

        public override SessionStateStoreData CreateNewStoreData(HttpContext context, int timeout)
        {
            try
            {
                return base.CreateNewStoreData(context, timeout);
            }
            catch (Exception ex)
            {
                LastException = ex;
                if (!ThrowOnError)
                    return null;
                throw;
            }
        }

        public override void CreateUninitializedItem(HttpContext context, string id, int timeout)
        {
            try
            {
                base.CreateUninitializedItem(context, id, timeout);
            }
            catch (Exception ex)
            {
                LastException = ex;
                if (!ThrowOnError)
                    return;
                throw;
            }            
        }

        public override SessionStateStoreData GetItem(HttpContext context, string id, out bool locked, out TimeSpan lockAge, out object lockId,
            out SessionStateActions actions)
        {
            try
            {
                return base.GetItem(context, id, out locked, out lockAge, out lockId, out actions);
            }
            catch (Exception ex)
            {
                LastException = ex;
                if (!ThrowOnError)
                {
                    locked = false;
                    lockAge = new TimeSpan();
                    lockId = null;
                    actions = SessionStateActions.None;
                    return null;
                }                    
                throw;
            }             
        }

        public override SessionStateStoreData GetItemExclusive(HttpContext context, string id, out bool locked, out TimeSpan lockAge,
            out object lockId, out SessionStateActions actions)
        {
            try
            {
                return base.GetItemExclusive(context, id, out locked, out lockAge, out lockId, out actions);
            }
            catch (Exception ex)
            {
                LastException = ex;
                if (!ThrowOnError)
                {
                    locked = false;
                    lockAge = new TimeSpan();
                    lockId = null;
                    actions = SessionStateActions.None;
                    return null;
                }
                throw;
            }               
        }

        public override void InitializeRequest(HttpContext context)
        {
            try
            {
                base.InitializeRequest(context);
            }
            catch (Exception ex)
            {
                LastException = ex;
                if (!ThrowOnError)
                {
                    return;
                }
                throw;
            }              
        }

        public override void ReleaseItemExclusive(HttpContext context, string id, object lockId)
        {
            try
            {
                base.ReleaseItemExclusive(context, id, lockId);
            }
            catch (Exception ex)
            {
                LastException = ex;
                if (!ThrowOnError)
                {
                    return;
                }
                throw;
            }              
        }

        public override void RemoveItem(HttpContext context, string id, object lockId, SessionStateStoreData item)
        {
            try
            {
                base.RemoveItem(context, id, lockId, item);
            }
            catch (Exception ex)
            {
                LastException = ex;
                if (!ThrowOnError)
                {
                    return;
                }
                throw;
            }             
        }

        public override void EndRequest(HttpContext context)
        {
            try
            {
                base.EndRequest(context);
            }
            catch (Exception ex)
            {
                LastException = ex;
                if (!ThrowOnError)
                {
                    return;
                }
                throw;
            }  
        }

        public override void ResetItemTimeout(HttpContext context, string id)
        {
            try
            {
                base.ResetItemTimeout(context, id);
            }
            catch (Exception ex)
            {
                LastException = ex;
                if (!ThrowOnError)
                {
                    return;
                }
                throw;
            }  
        }

        public override void SetAndReleaseItemExclusive(HttpContext context, string id, SessionStateStoreData item, object lockId, bool newItem)
        {
            try
            {
                base.SetAndReleaseItemExclusive(context, id, item, lockId, newItem);
            }
            catch (Exception ex)
            {
                LastException = ex;
                if (!ThrowOnError)
                {
                    return;
                }
                throw;
            }                          
        }

        public override bool SetItemExpireCallback(SessionStateItemExpireCallback expireCallback)
        {
            try
            {
                return base.SetItemExpireCallback(expireCallback);
            }
            catch (Exception ex)
            {
                LastException = ex;
                if (!ThrowOnError)
                {
                    return false;
                }
                throw;
            }            
        }

        protected override byte[] SerializeSessionState(SessionStateItemCollection sessionStateItems)
        {
            try
            {
                var items = new Dictionary<string, RedisData>();
                foreach (var key in sessionStateItems.Keys)
                {
                    items.Add((string)key, new RedisData() { Type = sessionStateItems[(string)key].GetType(), Value = sessionStateItems[(string)key] });
                }
                return System.Text.Encoding.UTF8.GetBytes(Serialize(items));
            }
            catch (Exception ex)
            {
                LastException = ex;
                if (!ThrowOnError)
                    return null;
                throw;
            }
        }

        protected override SessionStateItemCollection DeserializeSessionState(byte[] bytes)
        {
            try
            {
                var col = new SessionStateItemCollection();
                var stuff = Deserialize(System.Text.Encoding.UTF8.GetString(bytes));
                foreach (var key in stuff.Keys)
                {
                    if (stuff[key].Value == null || stuff[key].Type == null)
                    {
                        col[key] = null;
                    }
                    else
                    {
                        var a = stuff[key].Value as JObject;
                        if (a == null)
                        {
                            col[key] = stuff[key].Value;
                        }
                        else
                        {
                            col[key] = a.ToObject(stuff[key].Type);
                        }
                    }
                }
                return col;
            }
            catch (Exception ex)
            {
                LastException = ex;
                if (!ThrowOnError)
                    return null;
                throw;
            }
        }

        private Dictionary<string, RedisData> Deserialize(string serializedObj)
        {
            return JsonConvert.DeserializeObject<Dictionary<string, RedisData>>(serializedObj);
        }

        private string Serialize(Dictionary<string, RedisData> origObj)
        {
            return JsonConvert.SerializeObject(origObj); 
        }

        private class RedisData
        {
            public Type Type { get; set; }
            public Object Value { get; set; }
        }
    }
}
