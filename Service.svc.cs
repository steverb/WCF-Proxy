/// Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Web;
using System.ServiceModel.Activation;
using Microsoft.ServiceModel.Web;
using System.Linq;
using System.Net;
using System.Web;
using System.Configuration;
using System.IO;
using System.Text;

// The following line sets the default namespace for DataContract serialized typed to be ""
[assembly: ContractNamespace("", ClrNamespace = "Proxy2")]

namespace Proxy2
{
    // TODO: Please set IncludeExceptionDetailInFaults to false in production environments
    [ServiceBehavior(IncludeExceptionDetailInFaults = true), AspNetCompatibilityRequirements(RequirementsMode = AspNetCompatibilityRequirementsMode.Allowed), ServiceContract]
    public partial class Service
    {
        /// <summary>
        /// Returns data in response to a HTTP GET request with URIs of the form http://<url-for-svc-file>/GetData?param1=1&param2=hello
        /// By default, the response is in XML. To return JSON, set ResponseFormat to WebMessageFormat.Json in the WebGetAttribute
        /// </summary>
        /// <param name="i">param1 in the UriTemplate</param>
        /// <param name="s">param2 in the UriTemplate</param>
        /// <returns></returns>
        [WebGet(UriTemplate = "*")]
        [OperationContract]
        public Stream Get()
        {
            return ProxyThis();
        }

        //[WebInvoke(UriTemplate = "", Method = "POST")]
        //[OperationContract]
        //public string Post()
        //{
        //    // TODO: Add the new instance of SampleItem to the collection
        //    return "Post";
        //}



        //[WebInvoke(UriTemplate = "", Method = "PUT")]
        //[OperationContract]
        //public string Put()
        //{
        //    return "Put";
        //}

        //[WebInvoke(UriTemplate = "", Method = "DELETE")]
        //[OperationContract]
        //public void Delete()
        //{
        //    // TODO: Remove the instance of SampleItem with the given id from the collection
        //    throw new NotImplementedException();
        //}

        private Stream ProxyThis()
        {
            HttpRequest context = HttpContext.Current.Request;
            string myURL = ConfigurationManager.AppSettings["myURL"];
            string targetURL = context.RawUrl.Replace(myURL, "");
            string targetAddress = ConfigurationManager.AppSettings["targetAddress"] + targetURL;


            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(targetAddress);
            req.Method = context.HttpMethod;
            int Count = HttpContext.Current.Request.Headers.Count;
            req.Headers.Clear();
            for (int i = 0; i < Count; i++)
            {
                string key = HttpContext.Current.Request.Headers.GetKey(i);
                string keyValue = HttpContext.Current.Request.Headers.Get(i);

                //some headers have to be set via properties for unknown reasons.
                //some can't be set at all.
                switch (key)
                {
                    case "User-Agent":
                        req.UserAgent = keyValue;
                        break;
                    case "Connection":
                        //do nothing
                        break;
                    case "Close":
                        //do nothing
                        break;
                    case "Host":
                        break;
                    case "Accept":
                        req.Accept = keyValue;
                        break;
                    case "Referer":
                        req.Referer = keyValue;
                        break;
                    default:
                        req.Headers.Add(key, keyValue);

                        break;
                }
            }
            try
            {
                return HandleResponse((HttpWebResponse)req.GetResponse());
            }
            catch (WebException ex)
            {
                return HandleResponse((HttpWebResponse)ex.Response);
            }



        }

        private static Stream HandleResponse(HttpWebResponse backendResponse)
        {
            var response = WebOperationContext.Current.OutgoingResponse;
            response.StatusCode = backendResponse.StatusCode;
            using (var receiveStream = backendResponse.GetResponseStream())
            {


                // Copy headers
                // Check if header contains a contenth-lenght since IE
                // goes bananas if this is missing

                bool contentLenghtFound = false;
                foreach (string header in backendResponse.Headers)
                {
                    if (string.Compare(header,
                      "CONTENT-LENGTH", true) == 0)
                    {
                        contentLenghtFound = true;
                    }
                    response.Headers.Add(header, backendResponse.Headers[header]);
                }



                //do address translation
                string targetAddress = ConfigurationManager.AppSettings["targetAddress"];
                string myURL = ConfigurationManager.AppSettings["myURL"];
                Uri myURI = OperationContext.Current.RequestContext.RequestMessage.Headers.To;
                string myAddress = myURI.Scheme + @"://" + myURI.Authority + myURL;
                var reader = new StreamReader(receiveStream, System.Text.Encoding.UTF8);
                string received = reader.ReadToEnd();

                string output = received.Replace(targetAddress, myAddress);
                Stream ms = new MemoryStream(ASCIIEncoding.UTF8.GetBytes(output));


                // Add contentlength if it is missing
                if (!contentLenghtFound) response.ContentLength = ms.Length;

                // Set the stream to the start
                ms.Position = 0;
                return ms;
            }
        }
    }

   
}