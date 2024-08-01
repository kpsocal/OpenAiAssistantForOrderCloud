using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace OpenAIAssitant
{
    public static class OrderCloudHelper
    {
        private static string ClientId = "CC6EA030-1669-4072-97BB-971991B01B8A";
        private static string BaseAddress = "https://sandboxapi.ordercloud.io";       

        public static async Task<string> GetAuthTokenShopperAsync(string username, string password)
        {
            var httpClient = new HttpClient()
            {
                BaseAddress = new Uri(BaseAddress)
            };

            var nvc = new List<KeyValuePair<string, string>>();
            nvc.Add(new KeyValuePair<string, string>("grant_type", "password"));
            nvc.Add(new KeyValuePair<string, string>("client_id", ClientId));
            nvc.Add(new KeyValuePair<string, string>("username", username));
            nvc.Add(new KeyValuePair<string, string>("password", password));
            nvc.Add(new KeyValuePair<string, string>("scope", "Shopper MeAdmin PromotionReader MeCreditCardAdmin BuyerImpersonation"));

            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            HttpResponseMessage response = await httpClient.PostAsync("/oauth/token", new FormUrlEncodedContent(nvc));          
            
            if (response.IsSuccessStatusCode)
            {
                var result = response.Content.ReadAsStringAsync().Result;

                dynamic jsonData = JsonConvert.DeserializeObject<dynamic>(result);
                return jsonData.access_token;
            }           

            return null;

        }

        public static async Task<string> GetAuthTokenOrderAdminAsync(string username, string password)
        {
            var httpClient = new HttpClient()
            {
                BaseAddress = new Uri(BaseAddress)
            };

            var nvc = new List<KeyValuePair<string, string>>();
            nvc.Add(new KeyValuePair<string, string>("grant_type", "password"));
            nvc.Add(new KeyValuePair<string, string>("client_id", ClientId));
            nvc.Add(new KeyValuePair<string, string>("username", username));
            nvc.Add(new KeyValuePair<string, string>("password", password));
            nvc.Add(new KeyValuePair<string, string>("scope", "Shopper MeAdmin MeCreditCardAdmin OrderAdmin"));

            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            HttpResponseMessage response = await httpClient.PostAsync("/oauth/token", new FormUrlEncodedContent(nvc));          
            
            if (response.IsSuccessStatusCode)
            {
                var result = response.Content.ReadAsStringAsync().Result;

                dynamic jsonData = JsonConvert.DeserializeObject<dynamic>(result);
                return jsonData.access_token;
            }           

            return null;

        }

        public static async Task<string> GetAuthTokenProductAdminAsync(string username, string password)
        {
            var httpClient = new HttpClient()
            {
                BaseAddress = new Uri(BaseAddress)
            };

            var nvc = new List<KeyValuePair<string, string>>();
            nvc.Add(new KeyValuePair<string, string>("grant_type", "password"));
            nvc.Add(new KeyValuePair<string, string>("client_id", ClientId));
            nvc.Add(new KeyValuePair<string, string>("username", username));
            nvc.Add(new KeyValuePair<string, string>("password", password));
            nvc.Add(new KeyValuePair<string, string>("scope", "Shopper MeAdmin ProductAdmin"));

            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            HttpResponseMessage response = await httpClient.PostAsync("/oauth/token", new FormUrlEncodedContent(nvc));          
            
            if (response.IsSuccessStatusCode)
            {
                var result = response.Content.ReadAsStringAsync().Result;

                dynamic jsonData = JsonConvert.DeserializeObject<dynamic>(result);
                return jsonData.access_token;
            }           

            return null;

        }

        


        public static async Task<AddToCartResponse> AddToCartAsync(string oAuthToken, string productId, string cartId, int qty, string specId, string OptionId)
        {
            var specID = "";
            var optionID = "";
            var priceMarkupType = "";

            Console.WriteLine($"productId : {productId}");

            var productAdmin = GetAuthTokenProductAdminAsync("testing-prod-coll", "Peter12321!").Result;
            HttpClient clientAdmin = GetClient(productAdmin);

            HttpResponseMessage response = GetSpecOptionForProduct(productId, ref specID, ref optionID, ref priceMarkupType, clientAdmin);

            HttpClient client = GetClient(productAdmin);

            string request = "{\"ProductID\": \"{pid}\",\"ID\": \"{ID}\",\"Quantity\": 1,\"Specs\": [{\"SpecID\": \"{specId}\",\"OptionID\": \"{optionId}\",\"PriceMarkupType\": \"{markupType}\" }]}";

            request = request.Replace("{pid}", productId);
            request = request.Replace("{specId}", specID);
            request = request.Replace("{optionId}", optionID);
            request = request.Replace("{markupType}", priceMarkupType);
            var reqID = Guid.NewGuid().ToString().Replace("-", "");
            reqID = reqID.Replace("}", "");
            reqID = reqID.Replace("{", "");
            request = request.Replace("{ID}", reqID);


            response = await client.PostAsync("/v1/cart/lineitems", new StringContent(request, Encoding.UTF8, "application/json"));

            AddToCartResponse resp = new AddToCartResponse();
            resp.Success = "false";

            if (response.IsSuccessStatusCode)
            {
                response = clientAdmin.GetAsync($"v1/cart").Result;
                var result = response.Content.ReadAsStringAsync().Result;
                var jsonData = JsonConvert.DeserializeObject<Order>(result);
                resp.Currency = jsonData.Currency;
                resp.TotalItemsInCart = jsonData.LineItemCount.ToString();
                resp.CartTotal = string.Format("{0:N2}", jsonData.Total);
                resp.Success = "true";
                return resp;
            }
            else
            {
                Console.WriteLine($"ERROR IN ADD TO CART: {response.Content.ReadAsStringAsync().Result}");
            }

            return resp;

        }

        private static HttpResponseMessage GetSpecOptionForProduct(string productId, ref string specID, ref string optionID, ref string priceMarkupType, HttpClient clientAdmin)
        {
            HttpResponseMessage response = clientAdmin.GetAsync($"/v1/specs/productassignments?searchOn=ProductID&search={productId}").Result;
            if (response.IsSuccessStatusCode)
            {
                var result = response.Content.ReadAsStringAsync().Result;
                var jsonData = JsonConvert.DeserializeObject<ListPage<SpecProductAssignment>>(result);

                if (jsonData.Items.Any())
                {
                    specID = jsonData.Items.First().SpecID;
                    //Console.WriteLine($"specId : {specID}");
                }

                response = clientAdmin.GetAsync($"/v1/specs/{specID}/options").Result;
                if (response.IsSuccessStatusCode)
                {
                    result = response.Content.ReadAsStringAsync().Result;
                    var jsonOptionData = JsonConvert.DeserializeObject<ListPage<SpecOption>>(result);
                    optionID = jsonOptionData.Items.First().ID;
                    priceMarkupType = jsonOptionData.Items.First().PriceMarkupType.ToString();
                    //Console.WriteLine($"optionID : {optionID}");
                }

            }

            return response;
        }

        public static async Task<string> AddBillingAndShipping(string oAuthToken, string shippingId, string paymentAddressId)
        {
            HttpClient client = GetClient(oAuthToken);            

            string request = "{\"BillingAddressID\":\"IrvineOffice\",\"ShippingAddressID\": \"IrvineOffice\"}";
            
            HttpResponseMessage response = await client.PatchAsync("/v1/cart", new StringContent(request,Encoding.UTF8,"application/json"));          
            
            if (response.IsSuccessStatusCode)
            {
                var result = response.Content.ReadAsStringAsync().Result;
                //dynamic jsonData = JsonConvert.DeserializeObject<dynamic>(result);
                return result;
            }

            return "";
        }

        public static async Task<IList<ShipEstimate>> GetShippingOptionsAsync(string oAuthToken)
        {
            HttpClient client = GetClient(oAuthToken);                        
            
            HttpResponseMessage response = await client.PostAsync("/v1/cart/estimateshipping", null);          
            
            if (response.IsSuccessStatusCode)
            {
                var result = response.Content.ReadAsStringAsync().Result;
                var jsonData = JsonConvert.DeserializeObject<ShippingEstimatesResult>(result);
                var shippingEstimates = jsonData.ShipEstimateResponse;
                var estimates = shippingEstimates.ShipEstimates.ToList();
                return estimates; //
            }

            return null;


        }


        public static async Task<bool> DeleteCartAsync(string oAuthToken)
        {
            HttpClient client = GetClient(oAuthToken);                        
            
            HttpResponseMessage response = client.DeleteAsync("/v1/cart").Result;          
            
            if (response.IsSuccessStatusCode)
            {
                return true;
            }

            return false;

        }

        public static async Task<GetPriceResult> GetPriceAsync(string oAuthToken, string productId)
        {

            var specID = "";
            var optionID = "";
            var priceMarkupType = "";

            //Console.WriteLine($"GetPriceAsync: {productId}");

            GetPriceResult resultPrice = new GetPriceResult();

            HttpClient client = GetClient(oAuthToken);    

            HttpResponseMessage response = await client.GetAsync($"/v1/me/products/{productId}");          
            
            if (response.IsSuccessStatusCode)
            {
                var result = response.Content.ReadAsStringAsync().Result;
                var jsonData = JsonConvert.DeserializeObject<BuyerProduct>(result);
                var priceSchedule = jsonData.PriceSchedule;

                var price = priceSchedule.PriceBreaks?.FirstOrDefault(x => x.Quantity == 1)?.Price;
                decimal? optionPrice = 0;

                //Console.WriteLine($"GetPriceAsync: Calling GetAuthTokenProductAdminAsync");

                var productAdmin = GetAuthTokenProductAdminAsync("testing-prod-coll", "Peter12321!").Result;
                HttpClient clientAdmin = GetClient(productAdmin);

                var specresponse = GetSpecOptionForProduct(productId, ref specID, ref optionID, ref priceMarkupType, clientAdmin);

                //Console.WriteLine($"GetPriceAsync: Ending GetSpecOptionForProduct");

                if(!specresponse.IsSuccessStatusCode)
                  return resultPrice;                   


                response = await client.GetAsync($"/v1/me/products/{productId}/variants");

                if (response.IsSuccessStatusCode)
                {
                    var resultVariant = response.Content.ReadAsStringAsync().Result;
                    var data = JsonConvert.DeserializeObject<ListPage<Variant>>(resultVariant);

                    foreach (var item in data.Items)
                    {
                        var spec = item.Specs.Where(x => x.SpecID == specID && x.OptionID == optionID).FirstOrDefault();

                        if (spec != null)
                        {
                            optionPrice = spec.PriceMarkup;
                        }

                    }

                    resultPrice.Success = true;
                    resultPrice.Price = price + optionPrice;
                    resultPrice.Currency = priceSchedule.Currency;
                }
            }
            else
            {
                Console.WriteLine($"Error in Get Price: {response.Content.ReadAsStringAsync().Result}");
            }

            return resultPrice;

        }


        public static async Task<IList<Address>> GetAddressesAsync(string oAuthToken)
        {
            HttpClient client = GetClient(oAuthToken);                        
            
            HttpResponseMessage response = await client.GetAsync("/v1/me/addresses");          
            
            if (response.IsSuccessStatusCode)
            {
                var result = response.Content.ReadAsStringAsync().Result;
                var jsonData = JsonConvert.DeserializeObject<ListPage<Address>>(result);
                var addresses = jsonData.Items;

                List<Address> myAddresses = new List<Address>();
                foreach(var address in addresses)
                {                               
                    myAddresses.Add(address);
                }               
                return myAddresses; 
            }

            return null;

        }

        public static async Task<IList<CreditCard>> GetCreditCardsAsync(string oAuthToken)
        {
            HttpClient client = GetClient(oAuthToken);                        
            
            HttpResponseMessage response = await client.GetAsync("/v1/me/creditcards");          
            
            if (response.IsSuccessStatusCode)
            {
                var result = response.Content.ReadAsStringAsync().Result;
                var jsonData = JsonConvert.DeserializeObject<ListPage<CreditCard>>(result);
                var addresses = jsonData.Items;

                List<CreditCard> myCards = new List<CreditCard>();
                foreach(CreditCard card in addresses)
                {
                    myCards.Add(card);

                }               
                return myCards; //
            }

            return null;

        }


        public static async Task<SearchResults> GetSearchResultsAsync(string oAuthToken, string searchText)
        {
            HttpClient client = GetClient(oAuthToken);                        
            
            HttpResponseMessage response = await client.GetAsync($"/v1/me/products?catalogID=Default_XC_Headstart_Catalog&searchOn=Description&search={searchText}&pageSize=20");          
            
            if (response.IsSuccessStatusCode)
            {
                var result = response.Content.ReadAsStringAsync().Result;
                var jsonData = JsonConvert.DeserializeObject<ListPage<BuyerProduct>>(result);
                var products = jsonData.Items;

                SearchResults results = new SearchResults
                {
                    Products = new List<LeanProductModel>()
                };

                results.Count = jsonData.Meta.TotalCount;

                foreach (var product in products)
                {
                   var productInfo = new LeanProductModel();
                   productInfo.ProductName = product.Name;
                   productInfo.ProductID = product.ID;
                   productInfo.Description = product.Description;

                   results.Products.Add(productInfo);

                }               
                return results; //
            }

            return null;

        }

        public static async Task<AddCreditCardResult> CalcualteCartAndValidateAsync(string oAuthToken)
        {
            // Make sure CC payment is already added before this method is called
            HttpClient client = GetClient(oAuthToken);                        
            
            HttpResponseMessage response = await client.PostAsync("/v1/cart/calculate", null);          
            
            if (response.IsSuccessStatusCode)
            {
                 response = await client.PostAsync("/v1/cart/validate", null);
                if (response.IsSuccessStatusCode)
                {
                    if (response.IsSuccessStatusCode)
                    {
                        AddCreditCardResult finalResult = new AddCreditCardResult();

                        response = await client.GetAsync("/v1/cart");

                        if (response.IsSuccessStatusCode)
                        {
                            var result = response.Content.ReadAsStringAsync().Result;
                            var jsonData = JsonConvert.DeserializeObject<Order>(result);
                            finalResult.Success = "true";
                            finalResult.TotalItemsInCart = jsonData.LineItemCount.ToString();
                            finalResult.CartTotal = string.Format("{0:N2}", jsonData.Total);
                        }

                        return finalResult;
                    }
                }
            }               

            return null;

        }

        public static async Task<SubmitOrderResponse> SubmitCartAsync(string oAuthToken)
        {
            HttpClient client = GetClient(oAuthToken);

            SubmitOrderResponse message = new SubmitOrderResponse
            {
                IsSubmitted = false
            };

            HttpResponseMessage response = await client.PostAsync("/v1/cart/submit", null);          
            
            if (response.IsSuccessStatusCode)
            {
                var result = response.Content.ReadAsStringAsync().Result;
                var jsonData = JsonConvert.DeserializeObject<Order>(result);

                message.Currency = jsonData.Currency;
                message.OrderNumber = jsonData.ID;
                message.TotalItemsInCart = jsonData.LineItemCount.ToString();
                message.IsSubmitted = jsonData.IsSubmitted;
                message.Total = string.Format("{0:N2}", jsonData.Total);               
               
            }

            return message;

        }

        public static async Task<string> SetShippingMethodAsync(string oAuthToken, string shipEstimateId, string shipMethodId)
        {
            HttpClient client = GetClient(oAuthToken);          

            string request = "{\"ShipMethodSelections\": [{\"ShipEstimateID\":\"shp_65df6b19d44d42e58fc99052b97273eb\",\"ShipMethodID\": \"rate_7a21794dbc4940cd9b4a35108f16e9d6\"}]}";

            request = request.Replace("shp_65df6b19d44d42e58fc99052b97273eb", shipEstimateId)     ;       
            request = request.Replace("rate_7a21794dbc4940cd9b4a35108f16e9d6", shipMethodId)     ;  
                        

            HttpResponseMessage response = await client.PostAsync("/v1/cart/shipmethods", new StringContent(request,Encoding.UTF8,"application/json"));            
            
            if (response.IsSuccessStatusCode)
            {
                var result = response.Content.ReadAsStringAsync().Result;
                //dynamic jsonData = JsonConvert.DeserializeObject<dynamic>(result);
                return result;
            }

            return null;

        }

        public static async Task<bool> AddPaymentAndApprove(string oAuthToken, string creditCardId)
        {
            HttpClient client = GetClient(oAuthToken);            

            string request = "{\"Type\":\"CreditCard\",\"CreditCardID\":\"{creditCardId}\",\"Accepted\": true}";
            request = request.Replace("{creditCardId}", creditCardId);
            //string request = "{\"Type\":\"CreditCard\",\"CreditCardID\":\"eOaOFpid9U2pxkbuMhi03w\",\"Accepted\": true}";
            
            HttpResponseMessage response = await client.PostAsync("/v1/cart/Payments", new StringContent(request,Encoding.UTF8,"application/json"));          
            
            if (response.IsSuccessStatusCode)
            {
                var result = response.Content.ReadAsStringAsync().Result;
                dynamic jsonData = JsonConvert.DeserializeObject<dynamic>(result);
                return true;
            }

            return false;
        }


        public static HttpClient GetClient(string oAuthToken)
        {
            var httpClient = new HttpClient()
            {
                BaseAddress = new Uri(BaseAddress)
            };

            //httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Authorization", $"Bearer {oAuthToken}");
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {oAuthToken}");
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            return httpClient;
        }       
    }

    public class SubmitOrderResponse
    {
        public bool IsSubmitted {get;set;}
        public string Currency {get;set;}
        public string OrderNumber {get;set;}
        public string Total {get;set;}
        public string TotalItemsInCart {get;set;}

    }

    public class AddToCartResponse
    {
        public string Success {get;set;}
        public string Currency {get;set;}        
        public string CartTotal {get;set;}
        public string TotalItemsInCart {get;set;}
    }

    public class ShippingEstimatesResult
    {
        public ShipEstimateResponse ShipEstimateResponse;

        public ShippingEstimatesResult()
        {
            ShipEstimateResponse = new ShipEstimateResponse();
        }

    }


    public class GetPriceResult
    {
         public bool Success {get;set;}
         public decimal? Price {get;set;}
         public string Currency {get;set;}
        
    }

    public class SearchResults{
        public int Count;
        public List<LeanProductModel> Products;
        public bool Success;

    }

    public class LeanProductModel
    {
        public string ProductID;
        public string Description;      
        public string ProductName;          
    }
}



