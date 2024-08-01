using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;


namespace OpenAIAssitant
{
    class Program
    {
       

        static string AssistantId  = "asst_uEV5HtJb3XOkGkn9470Erobu"; 


       

        private static string ShopperToken = "";
        private static string OrderAdminToken = "";

        static string ThreadId = "";
        static async Task Main(string[] args)
        {
            
            var username = "";
            var password = "";

            
            ShopperToken = OrderCloudHelper.GetAuthTokenShopperAsync(username, password).Result;
            


            #region Setting up New HTTP Client
            var builder = new ConfigurationBuilder().AddUserSecrets<Program>();
                var config = builder.Build();
                var openAPIKey = config["openApiKey"];

                var httpClient = new HttpClient()
                {
                    BaseAddress = new Uri("https://api.openai.com/v1")
                };

                httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", openAPIKey);
                httpClient.DefaultRequestHeaders.Add("OpenAI-Beta", "assistants=v1");
                httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                #endregion

        


                ThreadId = await CreateThreadAsync(httpClient).ConfigureAwait(false);

                var message = "";

                while(message != "exit")
                {                    
                    message = ReadRequestResponse();
                    bool isSuccess = await AddMessageToThread(httpClient, ThreadId, message).ConfigureAwait(false);
                    if(isSuccess)
                    {
                        await ExecuteRunAsync(httpClient, ThreadId, AssistantId).ConfigureAwait(false);

                    }
                    else
                    {
                        //Console.WriteLine("error in adding message to thread");
                    }
                    // message = ReadRequestResponse();
                }


        }        

        private static async Task<string> CreateThreadAsync(HttpClient httpClient)
        {
            #region Create thread
            //Console.WriteLine("Creating thread...");
            var newThreadResponse = await httpClient.PostAsync("v1/threads", new StringContent("",
                                                                                                Encoding.UTF8,
                                                                                               "application/json"));
            Debug.Assert(newThreadResponse != null);
            newThreadResponse.EnsureSuccessStatusCode();
            var newThread = await newThreadResponse.Content.ReadFromJsonAsync<CreateThreadResult>();
            Debug.Assert(newThread != null);
            return newThread.Id;
            #endregion
        }

        private static async Task<bool> AddMessageToThread(HttpClient httpClient, string threadId, string message)
        {
            #region Add message

            try
            {
                //Console.WriteLine("Adding message...");
                var newMessageResponse = await httpClient.PostAsJsonAsync($"v1/threads/{threadId}/messages", new CreateThreadMessage(message));
                Debug.Assert(newMessageResponse != null);
                newMessageResponse.EnsureSuccessStatusCode() ;
            }
            catch(Exception ex)
            {
                Console.WriteLine($"AddMessageToThread failed... {ex.Message}");
                return false;
            }

            return true;
         
            #endregion
        }

        private static async Task ExecuteRunAsync(HttpClient httpClient, string threadId, string assistantId)
        {

            #region Create Run
            //Console.WriteLine("Creating run...");
            var newRunResponse = await httpClient.PostAsJsonAsync($"v1/threads/{threadId}/runs", new CreateRun(assistantId));
            
            Debug.Assert(newRunResponse != null);
            newRunResponse.EnsureSuccessStatusCode();
            var newRun = await newRunResponse.Content.ReadFromJsonAsync<Run>();

            
            Debug.Assert(newRun != null);
            #endregion

            #region Wait for completed
            var isToolResponseSubmitted = false;
            var loop = false;
            do
            {
                //Console.WriteLine("Waiting for run to complete...");
                var max = 15;
                while ((newRun.Status is not "completed" and not "requires_action" and not "failed" && max >= 0) || (newRun.Status is "requires_action" && isToolResponseSubmitted))
                {
                    try{
                        //Console.WriteLine("\tChecking run status...");
                        await Task.Delay(2500);
                        max--;
                        var runResponse = await httpClient.GetAsync($"v1/threads/{threadId}/runs/{newRun.Id}");
                        Debug.Assert(runResponse != null);                   
                        
                        runResponse.EnsureSuccessStatusCode();
                        //Console.WriteLine(await runResponse.Content.ReadAsStringAsync());
                        newRun = await runResponse.Content.ReadFromJsonAsync<Run>();
                        
                        Debug.Assert(newRun != null);
                        //Console.WriteLine($"\tRun status: {newRun.Status}");

                        if(max%2==0)
                        {
                            Console.WriteLine("Working on it.....");
                        }
                        else
                        {
                             Console.WriteLine("Just a little bit more...");
                        }
                    }
                    catch(Exception ex)
                    {
                     Console.WriteLine($" Error in ExecuteRunAsync {ex.Message}")   ;
                     break;
                    }
                    finally{
                        isToolResponseSubmitted = false;
                    }
                    
                }

                switch (newRun.Status)
                {
                    case "failed":
                     WriteAssitantResponse($"\t\tAn Error Occured {newRun.Status} {newRun.LastError}");
                    break;

                    case "completed":
                        {
                            //Console.WriteLine("\tListing messages of thread...");
                            var messages = await httpClient.GetFromJsonAsync<OaiResult<Message>>($"v1/threads/{threadId}/messages");
                            Debug.Assert(messages != null);
                            foreach (var m in messages.Data)
                            {
                                foreach (var c in m.Content)
                                {
                                    if(m.Role != "user")
                                    {
                                        WriteAssitantResponse($"{m.Role.ToUpper()}: {c.Text.Value}");
                                        loop = false;
                                        break;
                                    }
                                    //Console.WriteLine($"\t\t{m.Role}: {c.Text.Value}");                                    
                                }
                                break;
                            }

                            break;
                        }
                    case "requires_action":
                        {
                            WriteAssitantResponse("\tI need to fetch some more information, please give me a moment.");
                            var run = await httpClient.GetFromJsonAsync<Run>($"v1/threads/{threadId}/runs/{newRun.Id}");
                            Debug.Assert(run != null);
                            var functionName = run.RequiredAction.SubmitToolOutputs?.ToolCalls[0].Function.Name;
                            var arguments = run.RequiredAction.SubmitToolOutputs?.ToolCalls[0].Function.Arguments;
                            Debug.Assert(functionName != null);
                            Debug.Assert(arguments != null);
                            //Console.WriteLine($"\tFunction '{functionName}' called with arguments '{arguments}'");

                            switch(functionName){
                                case "GetPrice":
                                    var productId = JsonSerializer.Deserialize<GetPriceArguments>(arguments).productID;
                                    var getPriceMessage = GetPrice(productId);
                                    //Console.WriteLine($"Called GetPriceAsync {getPriceMessage.Success}");
                                    var getPriceResultmessage = Newtonsoft.Json.JsonConvert.SerializeObject(getPriceMessage);                                    

                                    HttpResponseMessage toolOutputResponseGetPrice = await PostResponse(httpClient, threadId, run, getPriceResultmessage).ConfigureAwait(false);
                                 

                                    Debug.Assert(toolOutputResponseGetPrice != null);
                                    toolOutputResponseGetPrice.EnsureSuccessStatusCode();
                                    isToolResponseSubmitted = true;
                                    max = 15;

                                    loop = true;
                                    break;


                                case "DeleteCart":
                                    
                                    var deleteCartMessage = OrderCloudHelper.DeleteCartAsync(ShopperToken).Result;
                                    var deleteCartResultmessage = Newtonsoft.Json.JsonConvert.SerializeObject(deleteCartMessage);                                    

                                    HttpResponseMessage toolOutputResponsedeleteCart = await PostResponse(httpClient, threadId, run, deleteCartResultmessage).ConfigureAwait(false);                                 

                                    Debug.Assert(toolOutputResponsedeleteCart != null);
                                    toolOutputResponsedeleteCart.EnsureSuccessStatusCode();
                                    isToolResponseSubmitted = true;
                                    max = 15;
                                    loop = true;
                                    break;



                                case "AddToCart":
                                    var addToCartMessage = AddToCart(JsonSerializer.Deserialize<AddToCartArguments>(arguments));
                                    var message = Newtonsoft.Json.JsonConvert.SerializeObject(addToCartMessage);
                                    //Console.WriteLine("Add To Cart Submit tool output to run...");

                                    HttpResponseMessage toolOutputResponseAddToCart = await PostResponse(httpClient, threadId, run, message).ConfigureAwait(false);

                                    Debug.Assert(toolOutputResponseAddToCart != null);
                                    toolOutputResponseAddToCart.EnsureSuccessStatusCode();
                                    isToolResponseSubmitted = true;
                                    max = 15;

                                    loop = true;
                                    break;

                                case "StartCheckout":
                                    var startCheckoutMessage = StartCheckout();
                                    var messageStartCheckout = Newtonsoft.Json.JsonConvert.SerializeObject(startCheckoutMessage);
                                    //Console.WriteLine("Add To Cart Submit tool output to run...");

                                    HttpResponseMessage toolOutputResponseStartCheckout = await PostResponse(httpClient, threadId, run, messageStartCheckout).ConfigureAwait(false);


                                    Debug.Assert(toolOutputResponseStartCheckout != null);
                                    toolOutputResponseStartCheckout.EnsureSuccessStatusCode();
                                    isToolResponseSubmitted = true;
                                    max = 15;

                                    loop = true;
                                    break;



                                case "AddBillingAndShipping":
                                    var addBillingShiipingToCartMessage = AddBillingAndShippingAddress(JsonSerializer.Deserialize<Address>(arguments));
                                    var messageBillingAndShipping = Newtonsoft.Json.JsonConvert.SerializeObject(addBillingShiipingToCartMessage);
                                    //Console.WriteLine("Add To Cart Submit tool output to run...");

                                    HttpResponseMessage toolOutputResponseBillingAndShiiping = await PostResponse(httpClient, threadId, run, messageBillingAndShipping).ConfigureAwait(false);


                                    Debug.Assert(toolOutputResponseBillingAndShiiping != null);
                                    toolOutputResponseBillingAndShiiping.EnsureSuccessStatusCode();

                                    isToolResponseSubmitted = true;
                                    max = 15;
                                    loop = true;
                                    break;

                                case "GetShippingMethods":
                                    var getShippingMethodMessage = GetShippingMethods();
                                    var messageGetShippingMethods = Newtonsoft.Json.JsonConvert.SerializeObject(getShippingMethodMessage);
                                    //Console.WriteLine("Add To Cart Submit tool output to run...");

                                    HttpResponseMessage toolOutputResponseGetShippingMethods = await PostResponse(httpClient, threadId, run, messageGetShippingMethods).ConfigureAwait(false);


                                    Debug.Assert(toolOutputResponseGetShippingMethods != null);
                                    toolOutputResponseGetShippingMethods.EnsureSuccessStatusCode();

                                    isToolResponseSubmitted = true;
                                    max = 15;
                                    loop = true;
                                    break;



                                case "SetShippingMethod":
                                    var setShippingMethodMessage = SetShippingMethod(JsonSerializer.Deserialize<ShipEstimatesArguments>(arguments));
                                    var messageSetShippingMethod = Newtonsoft.Json.JsonConvert.SerializeObject(setShippingMethodMessage);
                                    //Console.WriteLine("Add To Cart Submit tool output to run...");

                                    HttpResponseMessage toolOutputResponseSetShippingMethod = await PostResponse(httpClient, threadId, run, messageSetShippingMethod).ConfigureAwait(false);


                                    Debug.Assert(toolOutputResponseSetShippingMethod != null);
                                    toolOutputResponseSetShippingMethod.EnsureSuccessStatusCode();

                                    isToolResponseSubmitted = true;
                                    max = 15;
                                    loop = true;
                                    break;

                                

                                case "GetCreditCards":
                                    var getCreditCardsMessage = GetCreditCards();
                                    var messageGetCreditCards = Newtonsoft.Json.JsonConvert.SerializeObject(getCreditCardsMessage);
                                    //Console.WriteLine("Add To Cart Submit tool output to run...");

                                    HttpResponseMessage toolOutputResponseGetCreditCards = await PostResponse(httpClient, threadId, run, messageGetCreditCards).ConfigureAwait(false);


                                    Debug.Assert(toolOutputResponseGetCreditCards != null);
                                    toolOutputResponseGetCreditCards.EnsureSuccessStatusCode();

                                    isToolResponseSubmitted = true;
                                    max = 15;
                                    loop = true;
                                    break;

                                case "AddCreditCard" : 
                                var AddCreditCardMessage = AddCreditCard(JsonSerializer.Deserialize<SubmitOrderArguments>(arguments));
                                 //Console.WriteLine("SubmitOrder Submit tool output to run...");
                                  var creditCardMessage = Newtonsoft.Json.JsonConvert.SerializeObject(AddCreditCardMessage);

                                  HttpResponseMessage toolOutputResponseAddCreditCard = await PostResponse(httpClient, threadId, run, creditCardMessage).ConfigureAwait(false);
                                    // var toolOutputResponseSubmitOrder = await httpClient.PostAsJsonAsync($"v1/threads/{threadId}/runs/{newRun.Id}/submit_tool_outputs", new ToolsOutput(
                                    //     run.RequiredAction.SubmitToolOutputs!.ToolCalls[0].Id,  
                                    //     orderMessage
                                    // ));
                                    Debug.Assert(toolOutputResponseAddCreditCard != null);
                                    toolOutputResponseAddCreditCard.EnsureSuccessStatusCode();

                                  isToolResponseSubmitted = true;
                                    max = 15;

                                    loop = true;
                                    break;


                                case "SubmitOrder" : 
                                var submitOrderMessage = SubmitOrder();
                                 //Console.WriteLine("SubmitOrder Submit tool output to run...");
                                  var orderMessage = Newtonsoft.Json.JsonConvert.SerializeObject(submitOrderMessage);

                                  HttpResponseMessage toolOutputResponseSubmitOrder = await PostResponse(httpClient, threadId, run, orderMessage).ConfigureAwait(false);
                                    // var toolOutputResponseSubmitOrder = await httpClient.PostAsJsonAsync($"v1/threads/{threadId}/runs/{newRun.Id}/submit_tool_outputs", new ToolsOutput(
                                    //     run.RequiredAction.SubmitToolOutputs!.ToolCalls[0].Id,  
                                    //     orderMessage
                                    // ));
                                    Debug.Assert(toolOutputResponseSubmitOrder != null);
                                    toolOutputResponseSubmitOrder.EnsureSuccessStatusCode();

                                  isToolResponseSubmitted = true;
                                    max = 15;

                                    loop = true;
                                    break;


                                case "GetOrderDetails":
                                  var getOrderDetails = GetOrderDetails(JsonSerializer.Deserialize<GetOrderDetailsArguments>(arguments));
                                  
                                  var getOrderDetailsMessage = Newtonsoft.Json.JsonConvert.SerializeObject(getOrderDetails);

                                  HttpResponseMessage toolOutputResponse = await PostResponse(httpClient, threadId, run, getOrderDetailsMessage).ConfigureAwait(false);
                                    
                                    Debug.Assert(toolOutputResponse != null);
                                    toolOutputResponse.EnsureSuccessStatusCode();

                                   isToolResponseSubmitted = true;
                                    max = 15;

                                    loop = true;
                                    break;

                                case "SearchProducts":
                                    var SearchProductDetails = GetSearchResults(JsonSerializer.Deserialize<SearchArguments>(arguments));

                                    var searchProductDetailsMessage = Newtonsoft.Json.JsonConvert.SerializeObject(SearchProductDetails);

                                    HttpResponseMessage toolOutputResponseSearchProductDetails = await PostResponse(httpClient, threadId, run, searchProductDetailsMessage).ConfigureAwait(false);

                                    Debug.Assert(toolOutputResponseSearchProductDetails != null);
                                    toolOutputResponseSearchProductDetails.EnsureSuccessStatusCode();

                                    isToolResponseSubmitted = true;
                                    max = 15;

                                    loop = true;
                                    break;

                            }                         

                           

                            break;
                        }
                }
            }
            while (loop);
            #endregion


        }

        public static SearchResults GetSearchResults(SearchArguments arg)        
        {

            var result = OrderCloudHelper.GetSearchResultsAsync(ShopperToken, arg.searchText).Result;
            
            if(result != null)
            {
                result.Success = true;
            }
            else
            {
                result = new SearchResults() { Success = false };
            }

            return result;

        }

        private static List<CreditCard> GetCreditCards()
        {
            var repsonse = OrderCloudHelper.GetCreditCardsAsync(ShopperToken).Result;
            return repsonse?.ToList();
        }

        private static List<Address> StartCheckout()
        {
            var repsonse = OrderCloudHelper.GetAddressesAsync(ShopperToken).Result;
            return repsonse?.ToList();
        }

        private static GenericResult AddBillingAndShippingAddress(Address address)
        {
            var response = OrderCloudHelper.AddBillingAndShipping(ShopperToken, address.ID, address.ID).Result;
            
            if(!string.IsNullOrEmpty(response))
            {
                return new GenericResult() { Success = "true" };
            }
            else
            {
                return new GenericResult() { Success = "false" };
            }
        }


        private static GenericResult SetShippingMethod(ShipEstimatesArguments shipEstimate)
        {
            var setShipping = OrderCloudHelper.SetShippingMethodAsync(ShopperToken, shipEstimate.shipEstimateId, shipEstimate.shipMethodId).Result;
            //Console.WriteLine($"SetShippingMethodAsync Response:  {setShipping}");
            if (setShipping != null)
            {
                return new GenericResult() { Success = "true" };
            }
            else
                return new GenericResult() { Success = "false" };
        }

        private static async Task<HttpResponseMessage> PostResponse(HttpClient httpClient, string threadId, Run run, string message)
        {
            var tool_outputResponse = new List<tool_output>
                                 {
                                     new tool_output() { tool_call_id = run.RequiredAction.SubmitToolOutputs!.ToolCalls[0].Id, output = message }
                                 };

            var functionResponse = new functionResponses()
            {
                tool_outputs = tool_outputResponse
            };

            var payload = Newtonsoft.Json.JsonConvert.SerializeObject(functionResponse);
            //Console.WriteLine(payload);
            var content = new StringContent(payload, Encoding.UTF8, "application/json");

            var toolOutputResponseAddToCart = await httpClient.PostAsync($"v1/threads/{threadId}/runs/{run.Id}/submit_tool_outputs", content).ConfigureAwait(false);
            return toolOutputResponseAddToCart;
        }

        private static void WriteAssitantResponse(string response)
        {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine(response);
                Console.ForegroundColor = ConsoleColor.White;
        }

         private static string ReadRequestResponse()
        {
                Console.ForegroundColor = ConsoleColor.DarkGreen;
                Console.Write("Your message: ");
                var message = Console.ReadLine()!;
                Console.ForegroundColor = ConsoleColor.White;
                return message;
        }

        private static AddCreditCardResult AddCreditCard(SubmitOrderArguments submitOrderArguments)
        {
            OrderAdminToken = OrderCloudHelper.GetAuthTokenOrderAdminAsync("testing-prod-coll", "Peter12321!").Result;
            //Console.WriteLine($"GetAuthTokenOrderAdminAsync Response:  {OrderAdminToken}");
            var addCC = OrderCloudHelper.AddPaymentAndApprove(OrderAdminToken, submitOrderArguments.creditCardId).Result;
            //Console.WriteLine($"AddPaymentAndApprove Response:  {addCC}");
            if(addCC)
            {
                var validateCart = OrderCloudHelper.CalcualteCartAndValidateAsync(ShopperToken).Result;
                //Console.WriteLine($"CalcualteCartAndValidateAsync Response:  {validateCart}");

                return validateCart;
            }

            return new AddCreditCardResult {Success = "false"};
        }

        private static SubmitOrderResult SubmitOrder() 
        {           
            var submitCartResult = OrderCloudHelper.SubmitCartAsync(ShopperToken).Result;
            //Console.WriteLine($"SubmitCartAsync Response:  {submitCartResult}");

            return new SubmitOrderResult()
            {
                Success = submitCartResult.IsSubmitted.ToString(),
                orderTotal = submitCartResult.Total,
                expectedShippingDate = DateTime.Now.AddDays(2).ToLocalTime(),
                orderNumber = submitCartResult.OrderNumber,
                totalItemsInCart = submitCartResult.TotalItemsInCart,
                shippingMethod = "FED EX",
                currency = submitCartResult.Currency
            };
        }

        private static List<ShipEstimate> GetShippingMethods() 
        {
            var est = OrderCloudHelper.GetShippingOptionsAsync(ShopperToken).Result;
            //Console.WriteLine($"GetShippingOptionsAsync Response:  {est}");
           return est?.ToList();            
            
        }

        private static GetOrderDetailsResult GetOrderDetails(GetOrderDetailsArguments submitOrderArguments)
        {
            

               return new GetOrderDetailsResult()
            {
                Success = "true",
                 orderTotal = "33.67",
                 expectedShippingDate = DateTime.Now.AddDays(2).ToLocalTime(),
                 orderNumber = submitOrderArguments.orderNumber,
                 shippingMethod = "FED EX Ground",                
                 currency = "USD",
                 ShippingAddress = "2 Ventures, ste 340, Irvine, CA, 92618"
            };
        }

        private static AddToCartResult AddToCart(AddToCartArguments addToCartArguments)
        {            
            var re = OrderCloudHelper.AddToCartAsync(ShopperToken, addToCartArguments.productID, "KPTEST1233", Convert.ToInt32(addToCartArguments.quantity), "asd", "3q43").Result;            

            return new AddToCartResult()
            {
                Success = re.Success,
                cartTotal = re.CartTotal,
                currency = re.Currency,
                totalItemsInCart = re.TotalItemsInCart                
            };
        }

        private static GetPriceResult GetPrice(string productId)
        {            
            var re = OrderCloudHelper.GetPriceAsync(ShopperToken, productId).Result;            

            return new GetPriceResult()
            {
                Success = re.Success,
                Price = re.Price,
                Currency = re.Currency                           
            };
        }
    }

    public class  AddToCartResult{

        public string Success;
        public string cartTotal;
        public string currency;
        public string totalItemsInCart;
        public string promotionAdded;
    }

    public class AddCreditCardResult
    {
        public string Success;
        public string CartTotal;
        public string ShippingCost;
        public string TotalItemsInCart;
        //public List<LineDetails> LineItems;
    }

    public class LineDetails
    {
        public string ProductName;
    }

    public class SubmitOrderResult{

    public string Success;
    public string orderTotal;
    public string currency;
    public string orderNumber;
    public string totalItemsInCart;
    public DateTime expectedShippingDate;
    public string shippingMethod;
    
}

public class GetOrderDetailsResult{

    public string Success;
    public string orderTotal;
    public string currency;
    public string orderNumber;
    public string ShippingAddress;
    public DateTime expectedShippingDate;
    public string shippingMethod;
    
}


public class GenericResult{
     public string Success;
}


public class tool_output
{
    public string tool_call_id;
    public string output;
}


public class Response {
    public string threadId;
    public string runId;
    public List<tool_output> tool_outputs;

    //public functionResponses functionResponses;

}

    public class functionResponses
    {
        public List<tool_output> tool_outputs;

    }


}
