using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class Program
{
  private static string?
      _playerToken = Environment.GetEnvironmentVariable("PLAYER_TOKEN")
    , _levelIDToken = Environment.GetEnvironmentVariable("LEVEL_ID")
    , _instanceToken = ""
    , _entityID = "";
  private HttpClient _client = new HttpClient();

  public static async Task Main()
  {
    DotNetEnv.Env.Load("./.env");
    Program app = new Program();
    string? body = await app.CreateGame();
    if (body is null) app.CloseProgram();
    _entityID = GetEntityID(body);
    ClientWebSocket websocket = await ConnectWebsocket();
    bool subscribeSuccess = await SubscribeToGame(websocket);
    if (!subscribeSuccess) app.CloseProgram();
    Action action = new Action();
    while (true)
    {
      JObject? gameState = await WaitForNextGameTick(websocket);
      object payload = action.GenerateAction(gameState);
      Thread.Sleep(800);
      string message = CreateMessage(payload);
      await SendMessage(websocket, message);
      // TODO Detect game win or something and close program.
    }

    await websocket.CloseAsync(
      WebSocketCloseStatus.NormalClosure, "Done", CancellationToken.None);
  }

  private static string CreateMessage(object payload)
  {
    object data = new
    {
      gameId = _entityID,
      payload,
    };
    return JsonConvert.SerializeObject(new object[] { "run-command", data });
  }

  private static async Task<JObject?> WaitForNextGameTick(ClientWebSocket websocket)
  {
    while (websocket.State == WebSocketState.Open)
    {
      byte[] buffer = new byte[1024];
      WebSocketReceiveResult result = await websocket.ReceiveAsync(
        new ArraySegment<byte>(buffer), CancellationToken.None);
      if (result.MessageType == WebSocketMessageType.Text)
      {
        string receivedMsg = Encoding.UTF8.GetString(buffer, 0, result.Count);
        dynamic? dataObject = JsonConvert.DeserializeObject(receivedMsg);
        string temp = dataObject[1]["gameState"];
        JObject gameState = JsonConvert.DeserializeObject<JObject>(temp);
        return gameState;
      }
    }
    return null;
  }

  private static async Task<bool> SubscribeToGame(ClientWebSocket websocket)
  {
    if (websocket.State == WebSocketState.Open)
    {
      object payload = new { id = _entityID };
      string message = JsonConvert.SerializeObject(
        new object[] { "sub-game", payload });
      await SendMessage(websocket, message);
      return true;
    }
    return false;
  }

  private static async Task<ClientWebSocket> ConnectWebsocket()
  {
    var websocket = new ClientWebSocket();
    await websocket.ConnectAsync(new Uri(
      $"ws://goldrush.monad.fi/backend/{_playerToken}"), CancellationToken.None);
    return websocket;
  }

  private static async Task SendMessage(ClientWebSocket webSocket, string message)
  {
    byte[] messageBytes = Encoding.UTF8.GetBytes(message);
    await webSocket.SendAsync(
      new ArraySegment<byte>(messageBytes),
      WebSocketMessageType.Text,
      true,
      CancellationToken.None);
  }

  private async Task<string?> CreateGame()
  {
    _client.DefaultRequestHeaders.Add("Authorization", _playerToken);
    try
    {
      HttpResponseMessage response = await _client.PostAsync(
        $"https://goldrush.monad.fi/backend/api/levels/{_levelIDToken}", null);
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

  private static string GetEntityID(string data)
  {
    JObject? dataObject = JsonConvert.DeserializeObject<JObject>(data);
    return dataObject["entityId"].ToString();
  }

  private void CloseProgram()
  {
    Console.WriteLine("Good Bye");
    Environment.Exit(0);
  }
}




