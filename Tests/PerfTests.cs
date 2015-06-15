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
    public class PerfTests
    {
        private object validLockId;

        private const int timeout = 5;
        private Random random = new Randomizer(999);

        private const int NUMBER_OF_TIMES_TO_RUN_TESTS = 1000;

        [Test]
        public void lkaspnet_provider_save()
        {
            var prov = GetRedisSessionProvider();
            var context = CreateHttpContextWithSession(timeout);

            var startTime = DateTime.Now;

            bool isNew;
            var data = GetSessionDataOrCreateNew(prov, context, "abclk", out isNew);
            var newPerson = new Person
            {
                Name = "Daisy",
                Age = 0
            };
            data.Items["storedData0"] = newPerson;

            prov.SetAndReleaseItemExclusive(context, "abclk", data, validLockId, true);

            for (int i = 0; i < NUMBER_OF_TIMES_TO_RUN_TESTS; i++)
            {
                data = GetSessionDataOrCreateNew(prov, context, "abclk", out isNew);
                var randomInt = random.Next(0, 999);
                newPerson = new Person
                {
                    Name = "Daisy" + randomInt,
                    Age = randomInt
                };
                data.Items["storedData1"] = newPerson;
                data.Items["storedData2"] = newPerson;

                prov.SetAndReleaseItemExclusive(context, "abclk", data, validLockId, true);
            }

            var endTime = DateTime.Now;

            var runTime = endTime - startTime;

            Console.WriteLine("Run Time: " + runTime.TotalMilliseconds);
            prov.Dispose();
            prov = null;
            context = null;
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
    }
}
