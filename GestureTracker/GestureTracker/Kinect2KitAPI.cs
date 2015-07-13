using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using Newtonsoft.Json;

namespace GestureTracker
{
    class Kinect2KitAPI
    {
        public static string Server_Address { get; set; }

        public static readonly string API_New_Session = "/session/new";
        public static readonly string API_Acquire_Calibration = "calibration/acquire";
        public static readonly string API_Resolve_Calibration = "calibration/resolve";
        public static readonly string API_Start_Tracking = "/track/start";

        public static string URL_For(string api)
        {
            return Kinect2KitAPI.Server_Address + api;
        }

        public static dynamic GetPOSTResponseJSON(string api)
        {
            return Kinect2KitAPI.GetPOSTResponseJSON(api, new List<KeyValuePair<string, string>>());
        }

        public static dynamic GetPOSTResponseJSON(string api, List<KeyValuePair<string, string>> values)
        {
            string url = Kinect2KitAPI.URL_For(api);
            System.Diagnostics.Debug.WriteLine(url);
            using (var client = new HttpClient())
            {
                var data = new FormUrlEncodedContent(values);
                var response = client.PostAsync(url, data).Result;
                return JsonConvert.DeserializeObject(response.Content.ReadAsStringAsync().Result);
            };
        }
    }
}
