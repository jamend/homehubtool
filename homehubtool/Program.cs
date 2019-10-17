using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

// based on https://github.com/FransVDB/BBox3SagemTool

namespace homehubtool
{
	public static class Program
	{
		private static Uri _cgiReq;
		private static string _username;
		private static string _password;
		private static string _serverNonce;
		private static string _localNonce;
		private static int _sessionID;
		private static int _requestID;

		static void Main(string[] args)
		{
			if (args.Contains("--help") || (!args.Contains("--set") && !args.Contains("--get")))
			{
				Console.WriteLine(@"
Arguments:
--get [xpath]           Retrieve JSON dump of given xpath
--set [xpath] [value]   Set given xpath to given value
--url                   URL of router, default http://192.168.2.1
--username              Default admin
--password              Default admin
--help                  Show this help
");
				return;
			}

			var getIndex = Array.FindIndex(args, a => a.Equals("--get", StringComparison.CurrentCultureIgnoreCase));
			var getXpath = "";
			if (getIndex > -1)
			{
				if (args.Length < getIndex + 2 || string.IsNullOrEmpty(args[getIndex + 1]))
				{
					Console.WriteLine("Missing argument for --get");
					return;
				}

				getXpath = args[getIndex + 1];
			}

			var setIndex = Array.FindIndex(args, a => a.Equals("--set", StringComparison.CurrentCultureIgnoreCase));
			var setXpath = "";
			var setValue = "";
			if (setIndex > -1)
			{
				if (args.Length < setIndex + 3 || string.IsNullOrEmpty(args[setIndex + 1]))
				{
					Console.WriteLine("Missing arguments for --set");
					return;
				}

				setXpath = args[setIndex + 1];
				setValue = args[setIndex + 2];
			}

			var urlIndex = Array.FindIndex(args, a => a.Equals("--url", StringComparison.CurrentCultureIgnoreCase));
			string url = "http://192.168.2.1";
			if (urlIndex > -1)
			{
				if (args.Length < urlIndex + 2 || string.IsNullOrEmpty(args[urlIndex + 1]))
				{
					Console.WriteLine("Missing argument for --url");
					return;
				}

				url = args[urlIndex + 1];
			}

			var usernameIndex = Array.FindIndex(args, a => a.Equals("--username", StringComparison.CurrentCultureIgnoreCase));
			string username = "admin";
			if (usernameIndex > -1)
			{
				if (args.Length < usernameIndex + 2 || string.IsNullOrEmpty(args[usernameIndex + 1]))
				{
					Console.WriteLine("Missing argument for --username");
					return;
				}

				username = args[usernameIndex + 1];
			}

			var passwordIndex = Array.FindIndex(args, a => a.Equals("--password", StringComparison.CurrentCultureIgnoreCase));
			string password = "admin";
			if (passwordIndex > -1)
			{
				if (args.Length < passwordIndex + 2 || string.IsNullOrEmpty(args[passwordIndex + 1]))
				{
					Console.WriteLine("Missing argument for --password");
					return;
				}

				password = args[passwordIndex + 1];
			}

			OpenSession(url, username, password);

			if (!string.IsNullOrEmpty(getXpath))
			{
				var response = GetValue(getXpath);
				Console.WriteLine(response.Replace(",}", "}"));
			}
            
			if (!string.IsNullOrEmpty(setXpath))
			{
				var response = SetValue(setXpath, setValue);
				Console.WriteLine(response.Replace(",}", "}"));
			}
		}

		private static string GetValue(string xpath, int depth = 999)
		{
			return SendActionsToBBox(new List<Dictionary<string, object>>
			{
				new Dictionary<string, object>
				{
					{"id", 1},
					{"method", "getValue"},
					{"xpath", xpath},
					{
						"options", new Dictionary<string, object>
						{
							{"depth", depth}
						}
					}
				}
			});
		}

		private static string SetValue(string xpath, string value)
		{
			return SendActionsToBBox(new List<Dictionary<string, object>>
			{
				new Dictionary<string, object>
				{
					{"id", 1},
					{"method", "setValue"},
					{"xpath", xpath},
					{
						"parameters", new Dictionary<string, object>
						{
							{"value", value}
						}
					}
				}
			});
		}

        public static string GetLocalNonce()
        {
            return ((uint) new Random(DateTime.Now.Millisecond).Next(Int32.MaxValue)).ToString();
        }

        public static string Md5(string input)
        {
            MD5 md5Hash = MD5.Create();

            // Convert the input string to a byte array and compute the hash.
            byte[] data = md5Hash.ComputeHash(Encoding.UTF8.GetBytes(input));

            // Loop through each byte of the hashed data 
            // and format each one as a hexadecimal string.
            StringBuilder sBuilder = new StringBuilder();
            foreach (byte b in data)
                sBuilder.Append(b.ToString("x2"));

            // Return the hexadecimal string.
            return sBuilder.ToString();
        }

        public static string CalcAuthKey(string user, string password, int requestId, string nonce, string cNonce)
        {
            string ha1 = CalcHa1(user, password, nonce);
            return Md5(ha1 + ":" + requestId + ":" + cNonce + ":JSON:/cgi/json-req");
        }

        public static string CalcHa1(string user, string password, string nonce)
        {
            return Md5(user + ":" + nonce + ":" + Md5(password));
        }

        public static string CalcHa1Cookie(string user, string password, string nonce)
        {
            string ha1 = CalcHa1(user, password, nonce);
            return ha1.Substring(0, 10) + Md5(password) + ha1.Substring(10);
        }

        private static CookieCollection GetCookies()
        {
            var cookies = new CookieCollection {new Cookie("lang", "en", "/", _cgiReq.Host)};

            //set language cookie

            //set session cookie
            var jsonCookie = new
            {
                request = new Dictionary<string, object>
                {
                    {"req_id", (_requestID + 1)},
                    {"sess_id", _sessionID},
                    {"basic", false},
                    {"user", _username},
                    {"nonce", _serverNonce},
                    {"ha1", CalcHa1Cookie(_username, _password, _serverNonce)},
                    {
                        "dataModel",
                        new
                        {
                            name = "Internal",
                            nss = new[]
                            {
                                new
                                {
                                    name = "gtw",
                                    uri = "http://sagem.com/gateway-data"
                                }
                            }
                        }
                    }
                }
            };
            var cookieStr = JsonConvert.SerializeObject(jsonCookie);
            cookies.Add(new Cookie("session", HttpUtility.UrlEncode(cookieStr), "/", _cgiReq.Host));

            cookies[0].Expires = DateTime.Now.AddYears(1);
            cookies[1].Expires = DateTime.Now.AddDays(1);

            return cookies;
        }

        public static void OpenSession(string host, string username, string password)
        {
            _username = username;
            _password = password;
            var _bboxUrl = new Uri(host);
            _cgiReq = new Uri(_bboxUrl, Path.Combine("cgi", "json-req"));

            //reset member vars
            _sessionID = 0;
            _requestID = 0;
            _serverNonce = "";
            _localNonce = GetLocalNonce();

            //create json object
            var jsonLogin = new
            {
                request = new Dictionary<string, object>
                {
                    {"id", _requestID},
                    {"session-id", _sessionID.ToString()}, // !! must be string
                    {"priority", true},
                    {
                        "actions", new[]
                        {
                            new Dictionary<string, object>
                            {
                                {"id", 0},
                                {"method", "logIn"},
                                {
                                    "parameters", new Dictionary<string, object>
                                    {
                                        {"user", _username},
                                        //{"password", _password  },
                                        //{"basic", _basicAuth },
                                        {"persistent", "true"}, // !! must be string
                                        {
                                            "session-options", new Dictionary<string, object>
                                            {
                                                {
                                                    "nss", new[]
                                                    {
                                                        new
                                                        {
                                                            name = "gtw",
                                                            uri = "http://sagemcom.com/gateway-data"
                                                        }
                                                    }
                                                },
                                                {"language", "ident"},
                                                {
                                                    "context-flags", new Dictionary<string, object>
                                                    {
                                                        {"get-content-name", true}, //default true
                                                        {"local-time", true}, //default true
                                                        {"no-default", true} //default false
                                                    }
                                                },
                                                {"capability-depth", 0}, //default 1
                                                {
                                                    "capability-flags", new Dictionary<string, object>
                                                    {
                                                        {"name", false}, //default true
                                                        {"default-value", false}, //default true
                                                        {"restriction", false}, //default true
                                                        {"description", false}, //default false
                                                        {"flags", false}, //default true
                                                        {"type", false}, //default true

                                                    }
                                                },
                                                {"time-format", "ISO_8601"},
                                                {"depth", 6}, //default 2, 6 = refresh, 99 = debug
                                                {"max-add-events", 5},
                                                {"write-only-string", "_XMO_WRITE_ONLY_"},
                                                {"undefined-write-only-string", "_XMO_UNDEFINED_WRITE_ONLY_"}
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    },
                    {"cnonce", Convert.ToInt32(_localNonce)},
                    {"auth-key", CalcAuthKey(_username, _password, _requestID, _serverNonce, _localNonce)}
                }
            };

            //prepare data to send
            var jsonString = JsonConvert.SerializeObject(jsonLogin);
            var data = new Dictionary<string, string> {{"req", jsonString}};

            //send request & get response
            var response = SendRequest(_cgiReq, GetCookies(), data, "POST");

            //deserialize object
            var jsonObject = JsonConvert.DeserializeObject<JObject>(response);
            var parameters = jsonObject["reply"]["actions"][0]["callbacks"][0]["parameters"];

            //set session id and server nonce
            _sessionID = Convert.ToInt32(parameters["id"]);
            _serverNonce = Convert.ToString(parameters["nonce"]);
            _requestID++;
        }

        public static string SendRequest(Uri url, CookieCollection cookies = null, Dictionary<string, string> data = null, string mode = "GET")
        {
            try
            {
                HttpWebRequest request;

                //set data
                if (data != null)
                {
                    string dataStr = String.Join("&", data.Select(x => HttpUtility.UrlEncode(x.Key) + "=" + HttpUtility.UrlEncode(x.Value)));
                    //dataStr = dataStr.Replace("%5cu0026", "%26"); // &
                    dataStr = dataStr.Replace("%5cu0027", "%27"); // '
                    //dataStr = dataStr.Replace("%5cu003e", "%3e"); // >
                    //dataStr = dataStr.Replace("%5cu003c", "%3c"); // <
                    switch (mode)
                    {
                        case "GET":
                            url = new Uri(url + "?" + dataStr);
                            request = (HttpWebRequest) WebRequest.Create(url);
                            request.Method = "GET";
                            request.Host = url.Host;
                            break;
                        case "POST":
                            request = (HttpWebRequest) WebRequest.Create(url);
                            request.Method = "POST";
                            request.Host = url.Host;

                            //request.Referer = "http://192.168.1.1/2.5.7/gui/";

                            //thank you stackoverflow!
                            //http://stackoverflow.com/questions/566437/http-post-returns-the-error-417-expectation-failed-c
                            ServicePointManager.Expect100Continue = false;
                            request.ServicePoint.Expect100Continue = false;

                            // add post data to request
                            byte[] postBytes = Encoding.UTF8.GetBytes(dataStr);
                            request.ContentLength = postBytes.Length;
                            Stream postStream = request.GetRequestStream();
                            postStream.Write(postBytes, 0, postBytes.Length);
                            postStream.Close();

                            break;
                        default:
                            throw new NotImplementedException();
                    }
                }
                else
                    request = (HttpWebRequest) WebRequest.Create(url);

                //set headers, fake real browser the best we can
                request.KeepAlive = true;
                request.Accept = "application/json, text/javascript, */*; q=0.01";
                request.Headers.Add("Accept-Language", "nl,en-US;q=0.7,en;q=0.3");
                request.Headers.Add("Accept-Encoding", "gzip, deflate");
                request.Headers.Add("X-Requested-With", "XMLHttpRequest");
                request.Headers.Add("Pragma", "no-cache");
                request.Headers.Add("Cache-control", "no-cache");
                request.ContentType = "application/x-www-form-urlencoded; charset=UTF-8";
                request.UserAgent = "Mozilla/5.0 (Windows NT 6.3; Trident/7.0; rv:11.0) like Gecko"; //fake IE11

                //set cookies
                if (cookies != null)
                {
                    request.CookieContainer = new CookieContainer();
                    request.CookieContainer.Add(cookies);
                }

                //make request and get response
                WebResponse response = request.GetResponse();
                Stream responseStream = response.GetResponseStream();
                if (responseStream == null)
                    throw new Exception("No response from " + request.RequestUri);

                StreamReader reader = new StreamReader(responseStream);
                string responseString = reader.ReadToEnd();
                response.Close();
                return responseString;
            }
            catch (Exception ex)
            {
                Debugger.Log(0, "error", ex.ToString());
                return "";
            }
        }

        public static string SendActionsToBBox(List<Dictionary<string, object>> actions)
        {
            //calc local nonce
            _localNonce = GetLocalNonce();

            //create json object
            var jsonGetValue = new
            {
                request = new Dictionary<string, object>
                {
                    {"id", _requestID},
                    {"session-id", _sessionID},
                    {"priority", false},
                    {"actions", actions.ToArray()},
                    {"cnonce", Convert.ToUInt32(_localNonce)},
                    {"auth-key", CalcAuthKey(_username, _password, _requestID, _serverNonce, _localNonce)}
                }
            };

            //prepare data to send
            var jsonString = JsonConvert.SerializeObject(jsonGetValue);
            var data = new Dictionary<string, string> {{"req", jsonString}};

            //send request & get response
            var response = SendRequest(_cgiReq, GetCookies(), data, "POST");

            //increase request id
            _requestID++;

            return response.Replace(",}", "}");
        }
    }
}
