namespace PicklesCarSearch.Dialogs
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Bot.Builder.Dialogs;
    using Microsoft.Bot.Connector;
    using PicklesCarSearch.Model;

    [Serializable]
    public class CarouselCarsDialog : IDialog<object>
    {
        public async Task StartAsync(IDialogContext context)
        {
            context.Wait<Car[]>(this.MessageReceivedAsync);
        }

        public virtual async Task MessageReceivedAsync(IDialogContext context, IAwaitable<Car[]> result)
        {
            var msg = await result;

            var reply = context.MakeMessage();

            reply.AttachmentLayout = AttachmentLayoutTypes.Carousel;
            reply.Attachments = GetCardsAttachments(msg);

            await context.PostAsync(reply);

            //context.Wait<Car[]>(this.MessageReceivedAsync);
            context.Done<Car[]>(msg);
        }

        private static IList<Attachment> GetCardsAttachments(Car[] cars)
        {
            List<Attachment> items = new List<Attachment>();

            foreach (Car car in cars)
            {
                items.Add(GetHeroCard(car.make, car.model, 
                                      GetDescription(car.branchaddress, car.branchname, car.colour, car.price), 
                                      new CardImage(url: car.bloblurl), 
                                      new CardAction(ActionTypes.OpenUrl, "Learn more", value: "https://www.pickles.com.au/cars/item/-/details/CP-01-16--Built-01-16--Toyota--Corolla--ZRE182R-Ascent-S-CVT--Hatchback--5-Seats--5-Doors/402287710")));
            }

            return items;
        }

        private static string GetDescription(string branchaddress, string branchname, string colour, float price)
        {
            return $"Color: {colour}. \nPrice: {price}. \nBranch: {branchname}. \nAddress: {branchaddress}.";
        }

        private static Attachment GetHeroCard(string title, string subtitle, string text, CardImage cardImage, CardAction cardAction)
        {
            var heroCard = new HeroCard
            {
                Title = title,
                Subtitle = subtitle,
                Text = text,
                Images = new List<CardImage>() { cardImage },
                Buttons = new List<CardAction>() { cardAction },
            };

            return heroCard.ToAttachment();
        }

        private static Attachment GetThumbnailCard(string title, string subtitle, string text, CardImage cardImage, CardAction cardAction)
        {
            var heroCard = new ThumbnailCard
            {
                Title = title,
                Subtitle = subtitle,
                Text = text,
                Images = new List<CardImage>() { cardImage },
                Buttons = new List<CardAction>() { cardAction },
            };

            return heroCard.ToAttachment();
        }
    }
}
