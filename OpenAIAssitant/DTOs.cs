#region DTOs for OpenAI
// Note that normally, we would use Microsoft's Nuget package for OpenAI access (https://www.nuget.org/packages/Azure.AI.OpenAI). 
// However, the current version does not support the Beta APIs from OpenAI. Thererfore, we have to implement
// the DTOs ourselves. You can track the progress of the new OpenAI features in the Azure.AI.OpenAI package here:
// https://github.com/Azure/azure-sdk-for-net/issues/40347

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

record OaiResult<T>(
    T[] Data
);

record Assistant(
    string Name,
    string Description,
    string Model,
    string Instructions,
    FunctionToolEnvelope[] Tools)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string Id { get; set; }
}

record FunctionToolEnvelope(
    FunctionTool Function)
{
    public string Type => "function";
}

record FunctionTool(
    string Name,
    string Description,
    FunctionParameters Parameters
);

record FunctionParameters(
    Dictionary<string, FunctionParameter> Properties,
    string[] Required
)
{
    public string Type => "object";
}

record FunctionParameter(
    string Type,
    string Description
);

record CreateThread();

record CreateThreadResult(
    string Id
);

record CreateThreadMessage(
    string Content
)
{
    public string Role => "user";
}

record Message(
    string Id,
    MessageContent[] Content,
    [property: JsonPropertyName("thread_id")] string ThreadId,
    string Role,
    [property: JsonPropertyName("assistant_id")] string AssistantId,
    [property: JsonPropertyName("run_id")] string RunId
);

record MessageContent(
    MessageContentText Text
)
{
    public string Type => "text";
}

record MessageContentText(
    string Value
);

record CreateRun(
    [property: JsonPropertyName("assistant_id")] string AssistantId
);

record Run(
    string Id,
    [property: JsonPropertyName("thread_id")] string ThreadId,
    [property: JsonPropertyName("assistant_id")] string AssistantId,
    string Status,        
    [property: JsonPropertyName("required_action")] RequiredAction RequiredAction,
    [property: JsonPropertyName("last_error")] string LastError
);

record RequiredAction(
    string Type,
    [property: JsonPropertyName("submit_tool_outputs")] SubmitToolOutputs SubmitToolOutputs
);

record SubmitToolOutputs(
    [property: JsonPropertyName("tool_calls")] ToolCall[] ToolCalls
);

record ToolCall(
    string Id,
    FunctionToolCall Function
);

record FunctionToolCall(
    string Name,
    string Arguments
);

record VisitArguments(
    string TownName,
    string StreetName,
    string HouseNumber,
    string FamilyName,
    bool SuccessfullyVisited
);

record AddToCartArguments(
    string productID,
    string quantity 
);

record GetPriceArguments(
    string productID
);

record AddBillingAndShippingArguments(
    string AddressId    
);

record SubmitOrderArguments(    
    string creditCardId
);

record GetOrderDetailsArguments(
    string orderNumber    
);

record SearchArguments(
    string searchText    
);

record ShipEstimatesArguments(
    string shipEstimateId,
    string shipMethodId
);


record ToolsOutput(
    [property: JsonPropertyName("tool_call_id")] string ToolCallId,   
    string Output    
);



#endregion
