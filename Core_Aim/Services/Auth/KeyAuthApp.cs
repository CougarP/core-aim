using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Management;

namespace Core_Aim.Services.Auth
{
    public class KeyAuthApp
    {
        public string appname, ownerid, version;
        // O "Secret" foi removido nesta versão

        public KeyAuthApp(string name, string ownerid, string version)
        {
            this.appname = name;
            this.ownerid = ownerid;
            this.version = version;
        }

        public ApplicationSettings app_data = new ApplicationSettings();
        public UserData user_data = new UserData();
        public Response response = new Response();

        private string sessionid;
        private bool initialized;

        // ============================================================
        // INIT ATUALIZADO (SEM SECRET)
        // ============================================================
        public void init()
        {
            // Na versão 1.3, não encriptamos os dados de envio com o secret
            var values = new System.Collections.Specialized.NameValueCollection
            {
                ["type"] = "init",
                ["ver"] = version,
                ["hash"] = checksum(Process.GetCurrentProcess().MainModule.FileName),
                ["name"] = appname,
                ["ownerid"] = ownerid
                // Removemos "enckey" e "init_iv"
            };

            string response_string = req(values);

            if (response_string == "KeyAuth_Invalid")
            {
                error("Application not found");
                return;
            }

            // A resposta vem direta (sem decrypt com secret)
            var json = ResponseDecoder.string_to_generic<ResponseStructure>(response_string);
            load_response_struct(json);

            if (json.success)
            {
                load_app_data(json.appinfo);
                sessionid = json.sessionid;
                initialized = true;
            }
            else if (json.message == "invalidver")
            {
                app_data.downloadLink = json.appinfo.downloadLink;
            }
        }

        public void login(string username, string password)
        {
            if (!CheckInit()) return;
            string hwid = GetHardwareId();
            var values = new System.Collections.Specialized.NameValueCollection
            {
                ["type"] = "login",
                ["username"] = username,
                ["pass"] = password,
                ["hwid"] = hwid,
                ["sessionid"] = sessionid,
                ["name"] = appname,
                ["ownerid"] = ownerid
            };

            var response_string = req(values);
            var json = ResponseDecoder.string_to_generic<ResponseStructure>(response_string);
            load_response_struct(json);
            if (json.success) load_user_data(json.info);
        }

        public void register(string username, string password, string key)
        {
            if (!CheckInit()) return;
            string hwid = GetHardwareId();
            var values = new System.Collections.Specialized.NameValueCollection
            {
                ["type"] = "register",
                ["username"] = username,
                ["pass"] = password,
                ["key"] = key,
                ["hwid"] = hwid,
                ["sessionid"] = sessionid,
                ["name"] = appname,
                ["ownerid"] = ownerid
            };

            var response_string = req(values);
            var json = ResponseDecoder.string_to_generic<ResponseStructure>(response_string);
            load_response_struct(json);
            if (json.success) load_user_data(json.info);
        }

        public void license(string key)
        {
            if (!CheckInit()) return;
            string hwid = GetHardwareId();
            var values = new System.Collections.Specialized.NameValueCollection
            {
                ["type"] = "license",
                ["key"] = key,
                ["hwid"] = hwid,
                ["sessionid"] = sessionid,
                ["name"] = appname,
                ["ownerid"] = ownerid
            };

            var response_string = req(values);
            var json = ResponseDecoder.string_to_generic<ResponseStructure>(response_string);
            load_response_struct(json);
            if (json.success) load_user_data(json.info);
        }

        private bool CheckInit()
        {
            if (!initialized)
            {
                error("Please init first");
                return false;
            }
            return true;
        }

        public static string GetHardwareId()
        {
            try
            {
                string drive = Path.GetPathRoot(Environment.SystemDirectory).Substring(0, 1);
                ManagementObject dsk = new ManagementObject(@"win32_logicaldisk.deviceid=""" + drive + @":""");
                dsk.Get();
                string volumeSerial = dsk["VolumeSerialNumber"].ToString();
                return volumeSerial;
            }
            catch
            {
                return "HWID_ERROR_FALLBACK";
            }
        }

        private string req(System.Collections.Specialized.NameValueCollection post_data)
        {
            try
            {
                using (WebClient client = new WebClient())
                {
                    // Nota: A URL base pode ter mudado ligeiramente na 1.3, mas geralmente a 1.2 responde ou redireciona.
                    // Se der erro, tente mudar para /api/1.3/
                    var raw_response = client.UploadValues("https://keyauth.win/api/1.2/", post_data);
                    return Encoding.Default.GetString(raw_response);
                }
            }
            catch (WebException)
            {
                error("Connection failure. Check internet.");
                return "";
            }
        }

        private static string checksum(string filename)
        {
            using (var md5 = MD5.Create())
            {
                using (var stream = File.OpenRead(filename))
                {
                    var hash = md5.ComputeHash(stream);
                    return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }
            }
        }

        private void error(string message)
        {
            response.success = false;
            response.message = message;
        }

        public class ApplicationSettings
        {
            public string numUsers, numOnlineUsers, numKeys, version, customerPanelLink, downloadLink;
        }

        public class UserData
        {
            public string username, ip, hwid, createdate, lastlogin;
            public List<Data> subscriptions;
        }

        public class Data
        {
            public string subscription, expiry, timeleft;
        }

        public class Response
        {
            public bool success;
            public string message;
        }

        [DataContract]
        private class ResponseStructure
        {
            [DataMember] public bool success { get; set; }
            [DataMember] public string sessionid { get; set; }
            [DataMember] public string message { get; set; }
            [DataMember] public app_data_structure appinfo { get; set; }
            [DataMember] public user_data_structure info { get; set; }
        }

        [DataContract]
        private class app_data_structure
        {
            [DataMember] public string numUsers { get; set; }
            [DataMember] public string numOnlineUsers { get; set; }
            [DataMember] public string numKeys { get; set; }
            [DataMember] public string version { get; set; }
            [DataMember] public string customerPanelLink { get; set; }
            [DataMember] public string downloadLink { get; set; }
        }

        [DataContract]
        private class user_data_structure
        {
            [DataMember] public string username { get; set; }
            [DataMember] public string ip { get; set; }
            [DataMember] public string hwid { get; set; }
            [DataMember] public string createdate { get; set; }
            [DataMember] public string lastlogin { get; set; }
            [DataMember] public List<Data> subscriptions { get; set; }
        }

        private void load_app_data(app_data_structure data)
        {
            app_data.numUsers = data.numUsers;
            app_data.numOnlineUsers = data.numOnlineUsers;
            app_data.numKeys = data.numKeys;
            app_data.version = data.version;
            app_data.customerPanelLink = data.customerPanelLink;
        }

        private void load_user_data(user_data_structure data)
        {
            user_data.username = data.username;
            user_data.ip = data.ip;
            user_data.hwid = data.hwid;
            user_data.createdate = data.createdate;
            user_data.lastlogin = data.lastlogin;
            user_data.subscriptions = data.subscriptions;
        }

        private void load_response_struct(ResponseStructure data)
        {
            response.success = data.success;
            response.message = data.message;
        }
    }

    public static class ResponseDecoder
    {
        public static T string_to_generic<T>(string json)
        {
            using (var ms = new MemoryStream(Encoding.Default.GetBytes(json)))
            {
                var settings = new DataContractJsonSerializerSettings { UseSimpleDictionaryFormat = true };
                var serializer = new DataContractJsonSerializer(typeof(T), settings);
                return (T)serializer.ReadObject(ms);
            }
        }
    }
}