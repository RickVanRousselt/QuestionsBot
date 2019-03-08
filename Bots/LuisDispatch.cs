using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Advantive.Dispatcher.Office.Domain;
using AspNetCore_EchoBot_With_State;
using BotBuilder.Instrumentation;
using BotBuilder.Instrumentation.Instumentation;
using Microsoft.Bot;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Ai.LUIS;
using Microsoft.Bot.Builder.Ai.QnA;
using Microsoft.Bot.Builder.Core.Extensions;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

namespace Advantive.Dispatcher.Office.Bots
{
    public class LuisDispatch : IBot
    {

        public LuisDispatch(IConfiguration configuration)
        {
            var (luisModelId, luisSubscriptionKey, luisUri) = Startup.GetLuisConfiguration(configuration, "OfficeBot");
            this.luisModelOfficebot = new LuisModel(luisModelId, luisSubscriptionKey, luisUri);

          
            var (knowledgeBaseId, subscriptionKey, qnaUrl) = Startup.GetQnAMakerConfiguration(configuration);
            this.qnaEndpoint = new QnAMakerEndpoint
            {
                // add subscription key for QnA and knowledge base ID
                EndpointKey = subscriptionKey,
                KnowledgeBaseId = knowledgeBaseId,
                Host = qnaUrl
            };
        }

        private QnAMakerEndpoint qnaEndpoint;

        // App ID for a LUIS model named "OfficeBot"
        private LuisModel luisModelOfficebot;

        //// App ID for a LUIS model named "weather"
        //private LuisModel luisModelWeather;

        public async Task OnTurn(ITurnContext context)
        {
            if (context.Activity.Type is ActivityTypes.Message)
            {
                // Get the intent recognition result from the context object.
                var dispatchResult = context.Services.Get<RecognizerResult>(LuisRecognizerMiddleware.LuisRecognizerResultKey) as RecognizerResult;
                var topIntent = dispatchResult?.GetTopScoringIntent();

                if (topIntent == null)
                {
                    await context.SendActivity("Unable to get the top intent.");
                }
                else
                {
                    if (topIntent.Value.score < 0.3)
                    {
                        await context.SendActivity("I'm not very sure what you want but will try to send your request.");
                    }

                    await DispatchToTopIntent(context, topIntent);
                }
            }
            else if (context.Activity.Type is ActivityTypes.ConversationUpdate)
            {
                await WelcomeMembersAdded(context, "Hello, I'm Lisa. How can I help you");
               // await WelcomeMembersAdded(context, "Hello, I'm Lisa. I know a few things about Office. Do you have a question about that? ");
            }
        }

        private async Task DispatchToTopIntent(ITurnContext context, (string intent, double score)? topIntent)
        {
            switch (topIntent.Value.intent.ToLowerInvariant())
            {
                case "l_officebot":
                    await DispatchToLuisModel(context, this.luisModelOfficebot, "Officebot");

                    // Here, you can add code for calling the hypothetical home automation service, passing in any entity information that you need
                    break;

                case "none":
                // You can provide logic here to handle the known None intent (none of the above).
                // In this example we fall through to the QnA intent.
                case "q_officequestions":
                    await DispatchToQnAMaker(context, this.qnaEndpoint, "OfficeQuestions");
                    break;
                default:
                    // The intent didn't match any case, so just display the recognition results.
                    await context.SendActivity($"Dispatch intent: {topIntent.Value.intent} ({topIntent.Value.score}).");

                    break;
            }
        }

        private static async Task DispatchToQnAMaker(ITurnContext context, QnAMakerEndpoint qnaOptions, string appName)
        {
            QnAMaker qnaMaker = new QnAMaker(qnaOptions);
            if (!string.IsNullOrEmpty(context.Activity.Text))
            {
                var results = await qnaMaker.GetAnswers(context.Activity.Text.Trim()).ConfigureAwait(false);
                if (results.Any())
                {
                    await context.SendActivity(results.First().Answer);
                }
                else
                {
                   // await context.SendActivity($"Couldn't find an answer in the {appName}.");

                  var result = CallBingSearch(context.Activity.Text.Trim());
                    var attachments = new List<Attachment>();
                    for (int i = 0; i < 5; i++)
                    {
                        var hero = new HeroCard(
                                title: result.webPages.value[i].name,
                                images: new CardImage[]
                                    {new CardImage(url: result.webPages.value[i].openGraphImage.contentUrl)},
                                buttons: new CardAction[]
                                {
                                    new CardAction(title: "Open", type: ActionTypes.OpenUrl,
                                        value: result.webPages.value[i].url)
                                })
                            .ToAttachment();
                        attachments.Add(hero);
                    }
                    var activity = MessageFactory.Carousel(attachments.ToArray());
                    await context.SendActivity(activity);
                }
            }
        }


        private static BingCustomSearchResponse CallBingSearch(string query)
        {
            var subscriptionKey = "54f30cc78c4247be941a5542778ca377";
            var customConfigId = "3317468426";
            var searchTerm = query;

            var url = "https://api.cognitive.microsoft.com/bingcustomsearch/v7.0/search?" +
                      "q=" + searchTerm +
                      "&customconfig=" + customConfigId;

            var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", subscriptionKey);
            var httpResponseMessage = client.GetAsync(url).Result;
            var responseContent = httpResponseMessage.Content.ReadAsStringAsync().Result;
            BingCustomSearchResponse response = JsonConvert.DeserializeObject<BingCustomSearchResponse>(responseContent);

            return response;
        }

        private static async Task DispatchToLuisModel(ITurnContext context, LuisModel luisModel, string appName)
        {
            await context.SendActivity($"Sending your request to the {appName} system ...");
            var (intents, entities) = await RecognizeAsync(luisModel, context.Activity.Text);

            await context.SendActivity($"Intents detected by the {appName} app:\n\n{string.Join("\n\n", intents)}");

            if (entities.Count() > 0)
            {
                await context.SendActivity($"The following entities were found in the message:\n\n{string.Join("\n\n", entities)}");
            }
        }

        private static async Task<(IEnumerable<string> intents, IEnumerable<string> entities)> RecognizeAsync(LuisModel luisModel, string text)
        {
            var luisRecognizer = new LuisRecognizer(luisModel);
            var recognizerResult = await luisRecognizer.Recognize(text, System.Threading.CancellationToken.None);

            // list the intents
            var intents = new List<string>();
            foreach (var intent in recognizerResult.Intents)
            {
                intents.Add($"'{intent.Key}', score {intent.Value}");
            }

            // list the entities
            var entities = new List<string>();
            foreach (var entity in recognizerResult.Entities)
            {
                if (!entity.Key.ToString().Equals("$instance"))
                {
                    entities.Add($"{entity.Key}: {entity.Value.First}");
                }
            }

            return (intents, entities);
        }

        private static async Task WelcomeMembersAdded(ITurnContext context, string welcomeMessage)
        {
            foreach (var newMember in context.Activity.MembersAdded)
            {
                if (newMember.Id != context.Activity.Recipient.Id)
                {
                    await context.SendActivity(welcomeMessage);
                }
            }
        }
    }
}
