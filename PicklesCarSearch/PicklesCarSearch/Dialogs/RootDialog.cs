using System;
using System.Threading.Tasks;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Connector;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;
using PicklesCarSearch.Model;
using System.Collections.Generic;
using System.Threading;

namespace PicklesCarSearch.Dialogs
{
    [Serializable]
    public class RootDialog : IDialog<object>
    {
        List<string> years;
        string selectedMake;

        public Task StartAsync(IDialogContext context)
        {
            context.PostAsync("Welcome to Pickles Auctions. Please start your search by uploading a car image.");

            context.Wait(ImageReceivedAsync);

            return Task.CompletedTask;
        }

        private async Task ImageReceivedAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;

            if (activity.Attachments.Count > 0)
            {
                var contenttype = activity.Attachments[0].ContentType;
                var contentUrl = activity.Attachments[0].ContentUrl;

                string json = PrepareOptionsRequest(contentUrl);

                var options = GetOptions(json);

                List<string> makes;

                ExtractInfo(options, out makes, out years);

                if (makes.Count > 0 && years.Count > 0)
                {
                    PresentMakesOptions(context, makes);

                    await context.PostAsync($"Thanks, let's see what we have for your car...");
                }
                else
                {
                    await context.PostAsync($"Sorry, we couldn't find any cars matching this car. Please try again.");

                    context.Wait(this.ImageReceivedAsync);
                }
            }
        }

        private void PresentMakesOptions(IDialogContext context, List<string> makes)
        {
            PromptDialog.Choice(context, this.OnMakeSelected, makes, "Is your car one of those?", "Not a valid option", 3);
        }

        private async Task OnMakeSelected(IDialogContext context, IAwaitable<string> result)
        {
            try
            {
                this.selectedMake = await result;

                PresentYearsOptions(context, this.years);
            }
            catch (TooManyAttemptsException ex)
            {
                await context.PostAsync($"Ooops! Too many attemps :(. But don't worry, I'm handling that exception and you can try again!");

                context.Wait(this.ImageReceivedAsync);
            }
        }

        private void PresentYearsOptions(IDialogContext context, List<string> years)
        {
            PromptDialog.Choice(context, this.OnYearSelected, years, "Should be one of those right?", "Not a valid option", 3);
        }

        private async Task OnYearSelected(IDialogContext context, IAwaitable<string> result)
        {
            try
            {
                string selectedYear = await result;

                await context.PostAsync($"Please wait on while we get car information...");

                Car[] cars = this.GetCarData(this.selectedMake, selectedYear);

                await context.Forward(new CarouselCarsDialog(), this.ResumeAfterCarouselCarsDialog, cars, CancellationToken.None);
            }
            catch (TooManyAttemptsException ex)
            { 
                await context.PostAsync($"Ooops! Too many attemps :(. But don't worry, I'm handling that exception and you can try again!");

                context.Wait(this.ImageReceivedAsync);
            }
        }

        private async Task ResumeAfterCarouselCarsDialog(IDialogContext context, IAwaitable<object> result)
        {
            try
            {
                var message = await result;

                await context.PostAsync("Thanks for contacting our Pickles Auctions, come again!");
            }
            catch (Exception ex)
            {
                await context.PostAsync($"Failed with message: {ex.Message}");
            }
            finally
            {
                context.Wait(this.ImageReceivedAsync);
            }
        }

        private void ExtractInfo(Rootobject obj, out List<string> makes, out List<string> years)
        {
            int res;
            makes = new List<string>();
            years = new List<string>();

            foreach (Prediction pred in obj.Predictions)
            {
                if (!int.TryParse(pred.Tag, out res))
                {
                    if (pred.Probability > 0.8)
                    {
                        makes.Add(pred.Tag);
                    }
                }
                else
                {
                    if (pred.Probability > 0.8)
                    {
                        years.Add(pred.Tag);
                    }
                }
            }
        }

        private static string PrepareOptionsRequest(string contentUrl)
        {
            var webClient = new WebClient();

            byte[] imageBytes = webClient.DownloadData(contentUrl);

            var image = Convert.ToBase64String(imageBytes);

            var json = @"{ 'image':'" + image + "' }";

            return json;
        }

        public Rootobject GetOptions(string image)
        {
            string url = "https://pickles-image-scoring.azurewebsites.net/api/image-scorer?code=ZiZowjzslUfZjKR/fwP7HvriNILMJViaU8JhT1XMMMxjx1tlRirOkA==";

            HttpClient client = new HttpClient();

            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            HttpResponseMessage response = client.PostAsync(url, new StringContent(image, Encoding.UTF8, "application/json")).Result;
            
            if (response.IsSuccessStatusCode)
            {
                string result = response.Content.ReadAsStringAsync().Result;
                result = result.Substring(1);
                result = result.Substring(0, result.Length - 1);
                result = result.Replace("\\", "");
                return JsonConvert.DeserializeObject<Rootobject>(result);
            }
            else
            {
                return null;
            }
        }

        public Car[] GetCarData(string make, string year)
        {
            string url = "https://pickles-inventory.azurewebsites.net/api/HttpTriggerCSharp1?code=7gEt1hpX4bKr1/5zzzxR5x2Q4MwIssf7zDXhrDbY47HZ3At9wqgKMg==";

            HttpClient client = new HttpClient();

            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            
            HttpResponseMessage response = client.PostAsync(url, 
                                                            new StringContent(JsonConvert.SerializeObject(new CarInfo
                                                            {
                                                                Make = make,
                                                                Year = year
                                                            }), 
                                                            Encoding.UTF8, 
                                                            "application/json")).Result;

            if (response.IsSuccessStatusCode)
            {
                string result = response.Content.ReadAsStringAsync().Result;
                result = result.Substring(1);
                result = result.Substring(0, result.Length - 1);
                result = result.Replace("\\", "");
                result = result.Replace("W/\"datetime'", "");
                result = result.Replace("'\"", "");

                return JsonConvert.DeserializeObject<Car[]>(result);
            }
            else
            {
                return null;
            }
        }
    }
}