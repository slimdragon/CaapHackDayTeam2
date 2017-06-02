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
using System.IO;
using PicklesCarSearch.Services;

namespace PicklesCarSearch.Dialogs
{
    [Serializable]
    public class RootDialog : IDialog<object>
    {
        private readonly MicrosoftCognitiveSpeechService speechService = new MicrosoftCognitiveSpeechService();
        List<string> years;
        string selectedMake;

        public Task StartAsync(IDialogContext context)
        {
            context.PostAsync("Welcome to Pickles Auctions. Please start your search by uploading a car image.");

            context.Wait(MediaReceivedAsync);

            return Task.CompletedTask;
        }

        private async Task MediaReceivedAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;

            if (activity.Attachments.Count > 0)
            {
                var contenttype = activity.Attachments[0].ContentType;

                if (contenttype.Equals("application/octet-stream"))
                {
                    await HandleSpeech(context, activity.Attachments[0], activity.ServiceUrl);
                }
                else if (contenttype.Contains("image"))
                {
                    var contentUrl = activity.Attachments[0].ContentUrl;

                    await HandleImage(context, activity.Attachments[0], activity.ServiceUrl);
                }
            }
            else
            {
                if (activity.Type == ActivityTypes.Message)
                {
                    string make;
                    int year;

                    var text = activity.Text;

                    ProcessText(text, out make, out year);

                    if (make != null && year > 0)
                    {
                        Car[] cars = this.GetCarData(make, year.ToString());

                        if (cars.Length > 0)
                        {
                            await context.Forward(new CarouselCarsDialog(), this.ResumeAfterCarouselCarsDialog, cars, CancellationToken.None);
                        }
                        else
                        {
                            await context.PostAsync("Sorry, we couldn't find any cars matching your criteria. Try giving the car model and year only.");
                        }

                    }
                }
            }
        }

        private async Task HandleSpeech(IDialogContext context, Attachment attachment, string serviceUrl)
        {
            string make;
            int year;

            await context.PostAsync("Oh! Seems you want to search by talking to me :) Good Job, let's see what we have...");

            var connector = new ConnectorClient(new Uri(serviceUrl));

            var stream = await GetAudioStream(connector, attachment);

            var text = await this.speechService.GetTextFromAudioAsync(stream);

            ProcessText(text, out make, out year);

            if (make != null)
            {
                Car[] cars = this.GetCarData(make, year.ToString());

                await context.Forward(new CarouselCarsDialog(), this.ResumeAfterCarouselCarsDialog, cars, CancellationToken.None);
            }
        }

        private void ProcessText(string text, out string make, out int year)
        {
            make = null;
            year = -1;

            if (!string.IsNullOrEmpty(text))
            {
                var words = text.Split(' ');
                
                foreach(string word in words)
                {
                    if (int.TryParse(word, out year)){}
                    else
                    {
                        // check if the word is a car make using some congnitive service
                        make = word;
                    }
                   
                }
            }
        }

        private static async Task<Stream> GetAudioStream(ConnectorClient connector, Attachment audioAttachment)
        {
            using (var httpClient = new HttpClient())
            {
                var token = await (connector.Credentials as MicrosoftAppCredentials).GetTokenAsync();
                // The Skype attachment URLs are secured by JwtToken,
                // you should set the JwtToken of your bot as the authorization header for the GET request your bot initiates to fetch the image.
                // https://github.com/Microsoft/BotBuilder/issues/662
                var uri = new Uri(audioAttachment.ContentUrl);
                if (uri.Host.EndsWith("skype.com") && uri.Scheme == "https")
                {
                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                    httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));
                }

                return await httpClient.GetStreamAsync(uri);
            }
        }

        private async Task HandleImage(IDialogContext context, Attachment attachment, string serviceUrl)
        {
            string json = await PrepareOptionsRequest(attachment, serviceUrl);

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

                context.Wait(this.MediaReceivedAsync);
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

                context.Wait(this.MediaReceivedAsync);
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

                context.Wait(this.MediaReceivedAsync);
            }
        }

        private async Task ResumeAfterCarouselCarsDialog(IDialogContext context, IAwaitable<object> result)
        {
            try
            {
                var message = await result;

                PromptDialog.Choice(context, this.OnFinalDecision, new List<string>() { "Restart", "Finish" }, "Would you like to do another search? or call it off for the day?", "Not a valid option", 3);
            }
            catch (Exception ex)
            {
                await context.PostAsync($"Failed with message: {ex.Message}");
            }
        }

        private async Task OnFinalDecision(IDialogContext context, IAwaitable<string> result)
        {
            try
            {
                string selection = await result;

                if (selection == "Restart")
                {
                    context.Done("");
                }
                else
                {
                    await context.PostAsync("Thanks for contacting Pickles Auctions, come again!");
                }                
            }
            catch (TooManyAttemptsException ex)
            {
                await context.PostAsync($"Ooops! Too many attemps :(. But don't worry, I'm handling that exception and you can try again!");

                context.Done("");
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

        private async Task<string> PrepareOptionsRequest(Attachment attachment, string serviceUrl)
        {
            if (attachment?.ContentUrl != null)
            {
                using (var connectorClient = new ConnectorClient(new Uri(serviceUrl)))
                {
                    var token = await(connectorClient.Credentials as MicrosoftAppCredentials).GetTokenAsync();
                    var uri = new Uri(attachment.ContentUrl);
                    using (var httpClient = new HttpClient())
                    {
                        if (uri.Host.EndsWith("skype.com") && uri.Scheme == Uri.UriSchemeHttps)
                        {
                            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));
                        }
                        //else
                        //{
                        //    httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(attachment.ContentType));
                        //}

                        var attachmentData = await httpClient.GetByteArrayAsync(uri);

                        var image = Convert.ToBase64String(attachmentData);

                        var json = @"{ 'image':'" + image + "' }";

                        return json;
                    }
                }
            }

            return null;
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
            string url = "https://pickles-inventory.azurewebsites.net/api/pickles-inventory?code=rgew9MX0bTyLt7rfsX0y9NChqL7w1q6QaIulPPvTYAjrfbuaJpoaiA==";

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