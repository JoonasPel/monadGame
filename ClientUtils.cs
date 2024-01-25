using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public static class ClientUtils
{
  private static readonly string backendUrl = "goldrush.monad.fi/backend";
  private static string? _playerToken;
  private static string? _levelIdToken;
  private static string? _entityId;
  private static readonly HttpClient _client = new HttpClient();

  public static string CreateMessage(object payload)
  {
    object data = new
    {
      gameId = _entityId,
      payload,
    };
    return JsonConvert.SerializeObject(new object[] { "run-command", data });
  }

  public static async Task<JObject?> WaitForNextGameTick(ClientWebSocket websocket)
  {
    // TODO implement checking for game win and errors
    try
    {
      byte[] buffer = new byte[1024];
      WebSocketReceiveResult result = await websocket.ReceiveAsync(
        new ArraySegment<byte>(buffer), CancellationToken.None);
      if (result.MessageType == WebSocketMessageType.Text)
      {
        string receivedMsg = Encoding.UTF8.GetString(buffer, 0, result.Count);
        // TODO implement an actual class for the data object
        dynamic? dataObject = JsonConvert.DeserializeObject(receivedMsg);
        JObject tickData = dataObject[1];
        return tickData;
      }
    }
    catch (Exception e)
    {
      Console.WriteLine(
        $"Encountered Error while getting tick data from backend:\n{e.Message}");
      return null;
    }
    return null;
  }

  public static async Task<bool> SubscribeToGame(ClientWebSocket websocket)
  {
    if (websocket.State == WebSocketState.Open)
    {
      object payload = new { id = _entityId };
      string message = JsonConvert.SerializeObject(
        new object[] { "sub-game", payload });
      await SendMessage(websocket, message);
      return true;
    }
    return false;
  }

  public static async Task<ClientWebSocket> ConnectWebsocket()
  {
    var websocket = new ClientWebSocket();
    await websocket.ConnectAsync(new Uri(
      $"ws://{backendUrl}/{_playerToken}"), CancellationToken.None);
    return websocket;
  }

  public static async Task SendMessage(ClientWebSocket webSocket, string message)
  {
    byte[] messageBytes = Encoding.UTF8.GetBytes(message);
    await webSocket.SendAsync(
      new ArraySegment<byte>(messageBytes),
      WebSocketMessageType.Text,
      true,
      CancellationToken.None);
  }

  public static async Task<string?> CreateGame()
  {
    _client.DefaultRequestHeaders.Add("Authorization", _playerToken);
    try
    {
      HttpResponseMessage response = await _client.PostAsync(
        $"https://{backendUrl}/api/levels/{_levelIdToken}", null);
      response.EnsureSuccessStatusCode();
      string body = await response.Content.ReadAsStringAsync();
      return body;
    }
    catch (HttpRequestException ex)
    {
      Console.WriteLine($"Error Creating Game. {ex}");
      return null;
    }
  }

  public static void SetEntityId(string data)
  {
    JObject? dataObject = JsonConvert.DeserializeObject<JObject>(data);
    _entityId = dataObject["entityId"].ToString();
  }

  public static void SetTokens(string playerToken, string levelIdToken)
  {
    _playerToken = playerToken;
    _levelIdToken = levelIdToken;
  }
}