using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Connector;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace PicklesCarSearch.Model
{
    public class FlexiblePromptChoice<T> : PromptDialog.PromptChoice<T>
    {
        protected readonly PromptOptions<T> PromptOptions;

        public FlexiblePromptChoice(PromptOptions<T> promptOptions)
            : base(promptOptions)
        {
            this.PromptOptions = promptOptions;
        }

        protected override bool TryParse(IMessageActivity message, out T result)
        {
            //if (IsCancel(message.Text))
            //{
            //    result = default(T);
            //    return true;
            //}

            return base.TryParse(message, out result);
        }
    }
}