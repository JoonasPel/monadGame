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
    var websocket = new ClientWebSocket();
    await websocket.ConnectAsync(new Uri(
      $"ws://goldrush.monad.fi/backend/{_playerToken}"), CancellationToken.None);

    if (websocket.State == WebSocketState.Open)
    {
      object payload = new { id = _entityID };
      string message = JsonConvert.SerializeObject(
        new object[] { "sub-game", payload });
      await SendMessage(websocket, message);
    }

    while (websocket.State == WebSocketState.Open)
    {
      byte[] buffer = new byte[1024];
      Console.WriteLine("alive");

      WebSocketReceiveResult result = await websocket.ReceiveAsync(
        new ArraySegment<byte>(buffer), CancellationToken.None);
      if (result.MessageType == WebSocketMessageType.Text)
      {
        string receivedMsg = Encoding.UTF8.GetString(buffer, 0, result.Count);
        dynamic? dataObject = JsonConvert.DeserializeObject(receivedMsg);
        JObject? gameState = JsonConvert.DeserializeObject<JObject>(
          dataObject[1]["gameState"]);
        Console.WriteLine(gameState["player"]["position"]);
      }

      object data = new
      {
        gameId = _entityID,
        payload = new { action = "rotate", rotation = 45 }
      };
      string message = JsonConvert.SerializeObject(
        new object[] { "run-command", data });
      await SendMessage(websocket, message);

      Thread.Sleep(1000);
    }

    await websocket.CloseAsync(
      WebSocketCloseStatus.NormalClosure, "Done", CancellationToken.None);
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




