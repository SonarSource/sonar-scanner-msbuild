using System.IO;
using System.Net.Http;

namespace ConsoleApplication1
{
    class Program
    {
        static void Main(string[] args)
        {
            //Upload("http://localhost.:9000/api/profiles/restore", @"c:\temp\TestQualityProfile.xml");
            string s = "14.0";
            decimal ss = System.Convert.ToDecimal(s);

        }

        private static System.IO.Stream Upload(string url, string filename)
        {
            // Convert each of the three inputs into HttpContent objects

            var content = File.ReadAllText(filename);
            StringContent stringContent = new StringContent(content);


            using (var client = new HttpClient())
            using (var formData = new MultipartFormDataContent())
            {
                // Add the HttpContent objects to the form data
                client.DefaultRequestHeaders.Add("Authorization", "Basic YWRtaW46YWRtaW4 =");

                // <input type="text" name="filename" />
                formData.Add(stringContent, "backup", "TestQualityProfile.xml");


                // Actually invoke the request to the server

                // equivalent to (action="{url}" method="post")
                var response = client.PostAsync(url, formData).Result;

                // equivalent of pressing the submit button on the form
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }
                return response.Content.ReadAsStreamAsync().Result;
            }
        }
    }
}
