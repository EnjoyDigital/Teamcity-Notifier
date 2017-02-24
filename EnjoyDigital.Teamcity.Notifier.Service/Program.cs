using System;
using System.Configuration;
using StackExchange.Redis;
using TeamCitySharp;

namespace ConsoleApplication1
{
    class Program
    {
        static void Main(string[] args)
        {
            var redis = ConnectionMultiplexer.Connect(ConfigurationManager.AppSettings["RedisHost"]);

            var sub = redis.GetSubscriber();

            var client = new TeamCityClient(ConfigurationManager.AppSettings["TeamCityUrl"]);
            client.Connect("oliverpicton", "Sp1tf1rE");

            var lastBuild = client.Builds.ById(args[0]);           

            Console.WriteLine(lastBuild.BuildConfig);
                       
            if (lastBuild != null && lastBuild.Status == "FAILURE")
            {
                var message = String.Format("Call nine hundred and ninetey nine! {0} {1} build has failed we are all to blame.", lastBuild.BuildType.ProjectName, lastBuild.BuildType.Name);

                sub.Publish("teamcity_failedbuilds", message);
            }
        }
    }
}