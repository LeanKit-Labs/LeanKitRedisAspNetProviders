using System;
using System.Collections.Specialized;
using System.Configuration;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Web;
using System.Web.SessionState;
using LeanKit.Web.Redis;
using Moq;
using NUnit.Framework;

namespace Tests
{
    [TestFixture]
    public class Test
    {
        private object validLockId;

        private const int timeout = 20;
        private Random random = new Randomizer(999);

        [Test]
        public void uninitialized_item()
        {
            var prov = GetRedisSessionProvider();
            var context = CreateHttpContextWithSession(timeout);

            // testing this
            prov.CreateUninitializedItem(context, "abc1", timeout);

            var storedData = GetSessionData(prov, context, "abc1");
            Assert.IsNotNull(storedData);

            //prov.RemoveItem(context, "abc1", fakeLock, storedData);
        }

        [Test]
        public void store_object()
        {
            var prov = GetRedisSessionProvider();
            var context = CreateHttpContextWithSession(timeout);
            bool isNew;
            var data = GetSessionDataOrCreateNew(prov, context, "abc4", out isNew);

            var randomInt = random.Next(0, 999);
            var newPerson = new Person
            {
                Name = "Daisy" + randomInt,
                Age = randomInt
            };
            data.Items["storedData0"] = newPerson;
            data.Items["storedData1"] = 4;
            data.Items["storedData2"] = "hello new world";
            data.Items["storedData3"] = new AnotherPerson();

            // testing this
            prov.SetAndReleaseItemExclusive(context, "abc4", data, validLockId, true);

            var storedData = GetSessionData(prov, context, "abc4");
            Assert.IsNotNull(storedData);

            Assert.IsNotNull(storedData.Items["storedData0"]);
            Assert.IsTrue(storedData.Items["storedData0"].GetType() == typeof(Person));
            Assert.AreEqual(newPerson.Name, ((Person)storedData.Items["storedData0"]).Name);
            Assert.AreEqual(newPerson.Age, ((Person)storedData.Items["storedData0"]).Age);
            //Assert.IsNull(storedData.Items["storedData_json"]);

            //prov.RemoveItem(context, "abc4", fakeLock, storedData);
        }

        private SessionStateStoreData GetSessionDataOrCreateNew(SessionStateStoreProviderBase provider, HttpContext httpContext, string key, out bool isNew)
        {
            var data = GetSessionData(provider, httpContext, key);
            if (data == null)
            {
                data = provider.CreateNewStoreData(httpContext, timeout);
                isNew = true;
            }
            else
            {
                isNew = false;
                Console.WriteLine("Session key {0} already exists in Redis", key);
            }
            return data;
        }

        private SessionStateStoreData GetSessionData(SessionStateStoreProviderBase provider, HttpContext httpContext, string key)
        {
            bool locked = false;
            TimeSpan lockAge = new TimeSpan();
            SessionStateActions actions = new SessionStateActions();

            return provider.GetItem(httpContext, key, out locked, out lockAge, out validLockId, out actions);            
        }

        private SessionStateStoreProviderBase GetRedisSessionProvider()
        {
            var prov = new SessionStateStoreProvider();
            prov.Initialize("LeanKitTests", new NameValueCollection()
            {
                {"applicationName", "LeanKitTests"},
                {"host", ConfigurationManager.AppSettings.Get("redisServer")},
                {"port", ConfigurationManager.AppSettings.Get("redisPort")},
                {"databaseId", ConfigurationManager.AppSettings.Get("redisDbNumber")},
                {"ssl", "false"}, 
                {"throwOnError", "true"}
            });
            return prov;
        }

        private HttpContext CreateHttpContextWithSession(int sessionTimeout)
        {
            var httpRequest = new HttpRequest("foo.html", "http://localhost/foo.html", "");
            var httpResponse = new HttpResponse(TextWriter.Null);
            var httpContext = new HttpContext(httpRequest, httpResponse);

            // HACK: Initialize the session since we don't want to use the Web.config.
            var httpSession = (HttpSessionState) FormatterServices.GetUninitializedObject(typeof (HttpSessionState));
            typeof (HttpSessionState).GetField("_container", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(httpSession, new Mock<IHttpSessionState>().SetupAllProperties().Object);
            httpSession.Timeout = sessionTimeout;
            httpContext.Items["AspSession"] = httpSession;

            return httpContext;
        }

        [Serializable]
        private class Person
        {
            public string Name { get; set; }
            public int Age { get; set; }
        }

        [Serializable]
        private struct AnotherPerson
        {
            public string Name { get; set; }
            public string Age { get; set; }
        }
    }
}
