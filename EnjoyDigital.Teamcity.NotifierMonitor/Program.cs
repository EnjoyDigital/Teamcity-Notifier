using System;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Web;
using SlackAPI;
using StackExchange.Redis;

namespace EnjoyFailedTeamcityMonitor
{
    class Program
    {
        static ManualResetEvent _quitEvent = new ManualResetEvent(false);

        private static void Main()
        {
            //var shuffleResponse = httpClient.GetAsync(String.Format("/{0}/shuffle/on", room)).Result;

            //var playSongResponse = httpClient.GetAsync(String.Format("/{0}/playlist/{1}", room, "Broken%20Build")).Result;

            var clientId = "4217152087.144282245152";
            var clientSecret = "92bbce2e17425ab97c0e5f931993ade7";
            var redirectUri = "http://www.enjoy-digital.co.uk";

            Console.WriteLine("------------------------------------------------------------------");
            Console.WriteLine("This app will open your web browser pointing at an authentication");
            Console.WriteLine("page. When you complete authentication, you'll be sent back to ");
            Console.WriteLine("whatever 'redirectUri' is above, plus some query-string values. ");
            Console.WriteLine("Paste the URI into the console window when prompted.");
            Console.WriteLine();
            Console.WriteLine("In a proper web application, the user experience will obviously");
            Console.WriteLine("be more sensible...");
            Console.WriteLine("------------------------------------------------------------------");

            // start...
            var state = Guid.NewGuid().ToString();
            var uri = SlackClient.GetAuthorizeUri(clientId, SlackScope.Identify | SlackScope.Read | SlackScope.Post,
                redirectUri, state, "socialsaleslounge");
            Console.WriteLine("Directing to: " + uri);
            Process.Start(uri.ToString());

            Console.WriteLine("Paste in the URL of the authentication result...");
            var asString = Console.ReadLine();
            //var asString = "http://www.enjoy-digital.co.uk/?code=4217152087.144923517955.3d30a2bea5&state=d9402daf-b3e0-4b54-bdce-fea016c04ae7";

            var index = asString.IndexOf('?');
            if (index != -1)
                asString = asString.Substring(index + 1);

            // parse...
            var qs = HttpUtility.ParseQueryString(asString);
            var code = qs["code"];
            var newState = qs["state"];

            // validate the state. this isn't required, but it's makes sure the request and response line up...
            if (state != newState)
                throw new InvalidOperationException("State mismatch.");

            // then get the token...
            Console.WriteLine("Requesting access token...");
            SlackClient.GetAccessToken((response) =>
            {
                var redis = ConnectionMultiplexer.Connect(ConfigurationManager.AppSettings["TeamCityUrl"]);

                var sub = redis.GetSubscriber();

                sub.Subscribe("teamcity_failedbuilds", (channel, message) =>
                {
                    var httpClient = new HttpClient
                    {
                        BaseAddress = new Uri(ConfigurationManager.AppSettings["SonosApiUrl"])
                    };

                    var room = "developer%20dome";
                    var loudness = "60";

                    if (!bool.Parse(ConfigurationManager.AppSettings["TestMode"]))
                    {
                        var result = httpClient.GetAsync(String.Format("/{0}/say/{1}/{2}", room, message, loudness)).Result;

                        if (!result.IsSuccessStatusCode)
                        {
                            Console.WriteLine("An error occured calling the Sonos API");
                        }
                    }
                    var accessToken = response.access_token;
                    Console.WriteLine("Got access token '{0}'...", accessToken);

                    var client = new SlackClient(accessToken);
                    client.PostMessage(null, "#teamcity", message);                   
                    
                    client.GetUserList((ulr) =>
                    {
                        Console.WriteLine("got users");
                        var user = ulr.members.FirstOrDefault(a => a.name.Equals("oliverpicton"));
                        //var user = client.Users.Find(x => x.name.Equals("@oliverpicton"));
                        Console.WriteLine(user.name);

                        client.GetDirectMessageList(directMessages =>
                        {
                            foreach (var msg in directMessages.ims)
                            {
                                Console.WriteLine(msg.user);
                            }

                            var dmchannel = directMessages.ims.FirstOrDefault(x => x.user.Equals(user.id));

                            Console.WriteLine(dmchannel);

                            client.PostMessage((mr) => Console.WriteLine("sent! to " + dmchannel.id), dmchannel.id, message);
                        });                      
                    });

                    Console.WriteLine(message);
                });
            }, clientId, clientSecret, redirectUri, code);

            // finished...
            Console.WriteLine("Done.");

            Console.CancelKeyPress += (sender, eArgs) =>
            {
                _quitEvent.Set();
                eArgs.Cancel = true;
            };

            // kick off asynchronous stuff 

            _quitEvent.WaitOne();
        }
    }
}