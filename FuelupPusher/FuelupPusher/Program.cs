﻿//Copyright Microsoft 2014

using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using System.IO;
using System.Linq;

using PowerBIExtensionMethods;
using System.Web.Script.Serialization;
using System.Data.SqlClient;
using System.Data;
using System.Globalization;


namespace FuelupPusher
{

    //Sample to show how to use the Power BI API
    //  See also, http://docs.powerbi.apiary.io/reference

    //To run this sample

    //See How to register an app (http://go.microsoft.com/fwlink/?LinkId=519361)

    //Step 1 - Replace clientID with your client app ID. To learn how to get a client app ID, see How to register an app (http://go.microsoft.com/fwlink/?LinkId=519361)
    //Step 2 - Replace redirectUri with your redirect uri.

    class Program
    {
        //Step 1 - Replace client app ID 
        private static string clientID = "b1c41574-9e87-4252-b5a5-639afa042a28";

        //Step 2 - Replace redirectUri with the uri you used when you registered your app
        private static string redirectUri = "https://login.live.com/oauth20_desktop.srf";
        
        //Power BI resource uri
        private static string resourceUri = "https://analysis.windows.net/powerbi/api";             
        //OAuth2 authority
        private static string authority = "https://login.windows.net/common/oauth2/authorize";
        //Uri for Power BI datasets
        private static string datasetsUri = "https://api.powerbi.com/beta/myorg/datasets";

        private static AuthenticationContext authContext = null;
        private static string token = String.Empty;


        //.NET Class Example:
        private static string datasetName = "CarFuelups";
        private static string tableName = "Fuelups";

        static void Main(string[] args)
        {
            // Test the connection and update the datasetsUri in case of redirect
            datasetsUri = TestConnection();

            CreateDataset();

            List<Object> datasets = GetAllDatasets();

            foreach (Dictionary<string, object> obj in datasets)
            {
                Console.WriteLine(String.Format("id: {0} Name: {1}", obj["id"], obj["name"]));
            }

            //Initiate pushing of rows to Power BI
            Console.WriteLine("Press the Enter key to push rows into Power BI:");
            Console.ReadLine();
            AddClassRows();

            //Optional to test clear rows from a table
            //ClearRows();

            // Finished pushing rows to Power BI, close the console window
            Console.WriteLine("Data pushed to Power BI. Press the Enter key to close this window:");
            Console.ReadLine();

        }

        private static string TestConnection()
        {
            // Check the connection for redirects
            HttpWebRequest request = System.Net.WebRequest.Create(datasetsUri) as System.Net.HttpWebRequest;
            request.KeepAlive = true;
            request.Method = "GET";
            request.ContentLength = 0;
            request.ContentType = "application/json";
            request.Headers.Add("Authorization", String.Format("Bearer {0}", AccessToken));
            request.AllowAutoRedirect = false;

            HttpWebResponse response = (HttpWebResponse)request.GetResponse();
            if (response.StatusCode == HttpStatusCode.TemporaryRedirect)
            {
                return response.Headers["Location"];
            }
            return datasetsUri;

        }

        static string AccessToken
        {
            get
            {
                if (token == String.Empty)
                {
                    TokenCache TC = new TokenCache();
                    authContext = new AuthenticationContext(authority,TC);
                    token = authContext.AcquireToken(resourceUri, clientID, new Uri(redirectUri)).AccessToken.ToString();
                }
                else
                {
                    token = authContext.AcquireTokenSilent(resourceUri, clientID).AccessToken;
                }

                return token;
            }
        }

        static List<Object> GetAllDatasets()
        {
            List<Object> datasets = null;

            //In a production application, use more specific exception handling.
            try
            {
                //Create a GET web request to list all datasets
                HttpWebRequest request = DatasetRequest(datasetsUri, "GET", AccessToken);

                //Get HttpWebResponse from GET request
                string responseContent = GetResponse(request);

                //Get list from response
                datasets = responseContent.ToObject<List<Object>>();

            }
            catch (Exception ex)
            {               
                //In a production application, handle exception
            }

            return datasets;
        }

        static void CreateDataset()
        {
            //In a production application, use more specific exception handling.           
            try
            {               
                //Create a POST web request to list all datasets
                HttpWebRequest request = DatasetRequest(datasetsUri, "POST", AccessToken);

                var datasets = GetAllDatasets().Datasets(datasetName);

                if (datasets.Count() == 0)
                { 
                    //POST request using the json schema from Product
                    Console.WriteLine(new Fuelup().ToJsonSchema(datasetName));
                    Console.WriteLine(PostRequest(request, new Fuelup().ToJsonSchema(datasetName)));
                }
                else
                {
                    Console.WriteLine("Dataset exists");
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
            } 
        }

        static void AddClassRows()
        {
            //Get dataset id from a table name
            string datasetId = GetAllDatasets().Datasets(datasetName).First()["id"].ToString();

            //In a production application, use more specific exception handling. 
            try
            {
                HttpWebRequest request = DatasetRequest(String.Format("{0}/{1}/tables/{2}/rows", datasetsUri, datasetId, tableName), "POST", AccessToken);

                //Create a new fuelup
                Fuelup fuelup = ReadFuelupFromConsole();
                Console.WriteLine(fuelup.ToJson(JavaScriptConverter<Fuelup>.GetSerializer()));

                //POST request using the json from a list of Product
                //NOTE: Posting rows to a model that is not created through the Power BI API is not currently supported. 
                //      Please create a dataset by posting it through the API following the instructions on http://dev.powerbi.com.
                Console.WriteLine(PostRequest(request, fuelup.ToJson(JavaScriptConverter<Fuelup>.GetSerializer())));

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            } 
        }

        static Fuelup ReadFuelupFromConsole()
        {
            try
            {
                Console.Write("How many miles since your last fuelup? ");
                var miles = double.Parse(Console.ReadLine());
                Console.Write("How many of gallons was your fuelup? ");
                var gallons = double.Parse(Console.ReadLine());
                Console.Write("What is the cost of gas per gallon? ");
                var costPerGallon = double.Parse(Console.ReadLine());
                Console.Write("When was this fuelup (MM/DD/YYYY)? ");
                var date = DateTime.Parse(Console.ReadLine());
                Console.WriteLine("Your price for this fuelup was ${0} and your MPG was {0}", (costPerGallon * gallons), (miles / gallons));

                return new Fuelup
                {
                    Miles = miles,
                    Gallons = gallons,
                    PricePerGallon = costPerGallon,
                    FuelupDate = date,
                };
            }
            catch(Exception ex)
            {
                Console.WriteLine("Sorry I couldn't parse your input. Check that you inputted it correctly and try again");
                throw ex;
            }
        }

        static void ClearRows()
        {
            //Get dataset id from a table name
            string datasetId = GetAllDatasets().Datasets(datasetName).First()["id"].ToString();

            //In a production application, use more specific exception handling. 
            try
            {
                //Create a DELETE web request
                HttpWebRequest request = DatasetRequest(String.Format("{0}/{1}/tables/{2}/rows", datasetsUri, datasetId, tableName), "DELETE", AccessToken);
                request.ContentLength = 0;

                Console.WriteLine(GetResponse(request));
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            } 
        }

        private static string PostRequest(HttpWebRequest request, string json)
        {
            byte[] byteArray = System.Text.Encoding.UTF8.GetBytes(json);
            request.ContentLength = byteArray.Length;

            //Write JSON byte[] into a Stream
            using (Stream writer = request.GetRequestStream())
            {
                writer.Write(byteArray, 0, byteArray.Length);
            }
            return GetResponse(request);
        }

        private static string GetResponse(HttpWebRequest request)
        {
            string response = string.Empty;

            using (HttpWebResponse httpResponse = request.GetResponse() as System.Net.HttpWebResponse)
            {
                //Get StreamReader that holds the response stream
                using (StreamReader reader = new System.IO.StreamReader(httpResponse.GetResponseStream()))
                {
                    response = reader.ReadToEnd();                 
                }
            }

            return response;
        }

        private static HttpWebRequest DatasetRequest(string datasetsUri, string method, string authorizationToken)
        {
            HttpWebRequest request = System.Net.WebRequest.Create(datasetsUri) as System.Net.HttpWebRequest;
            request.KeepAlive = true;
            request.Method = method;
            request.ContentLength = 0;
            request.ContentType = "application/json";
            request.Headers.Add("Authorization", String.Format( "Bearer {0}", authorizationToken));

            return request;
        }
    }
}
