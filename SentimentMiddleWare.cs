using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.CognitiveServices.Language.TextAnalytics;
using Microsoft.Azure.CognitiveServices.Language.TextAnalytics.Models;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Configuration;
using Microsoft.Rest;

namespace Advantive.Dispatcher.Office
{
    public class SentimentMiddleware : IMiddleware
    {
        public SentimentMiddleware(IConfiguration configuration)
        {
            ApiKey = configuration.GetValue<string>("SentimentKey");
        }

        public string ApiKey { get; }

        public async Task OnTurn(ITurnContext context, MiddlewareSet.NextDelegate next)
        {
            if (context.Activity.Type is ActivityTypes.Message)
            {
                if (string.IsNullOrEmpty(context.Activity.Text))
                {
                    context.Services.Add<string>("0.0");
                }

                // Create a client
                var client = new TextAnalyticsAPI(new ApiKeyServiceClientCredentials(ApiKey));

                client.AzureRegion = AzureRegions.Westeurope;

                // Extract the language
                var result = await client.DetectLanguageAsync(new BatchInput(new List<Input>() { new Input("1", context.Activity.Text) }));
                var language = result.Documents?[0].DetectedLanguages?[0].Name;

                // Get the sentiment
                var sentimentResult = await client.SentimentAsync(
                    new MultiLanguageBatchInput(
                        new List<MultiLanguageInput>()
                        {
                            new MultiLanguageInput("en", "0", context.Activity.Text),

                        }));

                context.Services.Add<string>(sentimentResult.Documents?[0].Score?.ToString("#.#"));
            }

            await next();
        }
    }

    class ApiKeyServiceClientCredentials : ServiceClientCredentials
    {
        string SubscriptionKey { get; set; }
        public ApiKeyServiceClientCredentials(string subscriptionKey)
        {
            SubscriptionKey = subscriptionKey;
        }

        public override Task ProcessHttpRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.Headers.Add("Ocp-Apim-Subscription-Key", SubscriptionKey);
            return base.ProcessHttpRequestAsync(request, cancellationToken);
        }
    }
}
