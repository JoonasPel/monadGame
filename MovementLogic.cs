using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class MovementLogic
{
  private int[] _rotations = new int[] { 0, 45, 90, 135, 180, 225, 270, 315 };
  private Dictionary<(int, int), int> _visitedSquaresCount;
  private bool _nextActionIsMove;
  // key = square, value = array of squares we can go from key square
  private Dictionary<(int, int), List<(int, int)>> _graph;
  private (int, int) _target;
  private int _currentRotation = 0;
  private (int x, int y) _currentPosition = (0, 0);

  public MovementLogic(JObject gameData)
  {
    _visitedSquaresCount = new Dictionary<(int, int), int>();
    _nextActionIsMove = false;
    _graph = new Dictionary<(int, int), List<(int, int)>>();
    try
    {
      string temp = gameData["gameState"].ToString();
      JObject gameState = JsonConvert.DeserializeObject<JObject>(temp);
      _target = ((int)gameState["target"]["x"], (int)gameState["target"]["y"]);
    }
    catch (Exception e)
    {
      Console.WriteLine($"Error getting target:\n{e.Message}");
    }
  }

  private object MoveAction()
  {
    return new { action = "move" };
  }

  private object RotateAction(int newRotation)
  {
    return new { action = "rotate", rotation = newRotation };
  }

  public object ResetAction()
  {
    return new { action = "reset" };
  }

  private double EuclidianDistance(int x1, int x2, int y1, int y2)
  {
    return Math.Sqrt((Math.Pow(x1 - x2, 2) + Math.Pow(y1 - y2, 2)));
  }

  private void UpdateGraph((int, int) oldPos, (int, int) newPos)
  {
    if (_graph.TryGetValue(oldPos, out List<(int, int)> nextPositions))
    {
      if (!_graph[oldPos].Contains(newPos))
      {
        _graph[oldPos].Add(newPos);
      }
    }
    else
    {
      _graph[oldPos] = new List<(int, int)> { newPos };
    }
  }

  public void PrintGraph()
  {
    foreach (var pos in _graph)
    {
      Console.Write($"{pos.Key} => ");
      foreach (var nextPos in pos.Value)
      {
        Console.Write($"{nextPos} ");
      }
      Console.WriteLine();
    }
  }

  public object? GenerateActionPhase1(JObject tickData, out bool targetFound)
  {
    targetFound = false;
    try
    {
      // Check if last round we decided to move next (this) round
      if (_nextActionIsMove == true)
      {
        _nextActionIsMove = false;
        return MoveAction();
      }
      string temp = tickData["gameState"].ToString();
      JObject gameState = JsonConvert.DeserializeObject<JObject>(temp);
      int walls = (int)gameState["square"];
      string wallsBinary = Convert.ToString(walls, 2);
      string wallsPadded = wallsBinary.PadLeft(4, '0');
      int currentRotation = (int)gameState["player"]["rotation"];
      int posX = (int)gameState["player"]["position"]["x"];
      int posY = (int)gameState["player"]["position"]["y"];
      int targetX = (int)gameState["target"]["x"];
      int targetY = (int)gameState["target"]["y"];

      _visitedSquaresCount.TryGetValue((posX, posY), out int count);
      _visitedSquaresCount[(posX, posY)] = count + 1;
      var possibleNewPositions = new PriorityQueue<(int, int), double>();
      // check walls and see where we can go + calculate the priority
      if (wallsPadded[1] == '0') CheckPossiblePos(posX + 1, posY);
      if (wallsPadded[3] == '0') CheckPossiblePos(posX - 1, posY);
      if (wallsPadded[0] == '0') CheckPossiblePos(posX, posY - 1);
      if (wallsPadded[2] == '0') CheckPossiblePos(posX, posY + 1);
      void CheckPossiblePos(int newPosX, int newPosY)
      {
        UpdateGraph((posX, posY), (newPosX, newPosY));
        double dist = EuclidianDistance(newPosX, targetX, newPosY, targetY);
        _visitedSquaresCount.TryGetValue((newPosX, newPosY), out int visitCount);
        // weighting 2.5 is just a number from the sky
        possibleNewPositions.Enqueue((newPosX, newPosY), dist + 2.5 * visitCount);
      }
      // get best square to go
      (int newPosX, int newPosY) = possibleNewPositions.Dequeue();
      if ((newPosX, newPosY) == _target)
      {
        targetFound = true;
        return null;
      }
      return CreateNeededAction((posX, posY), (newPosX, newPosY), currentRotation);
    }
    catch (Exception e)
    {
      Console.WriteLine(
        $"Encountered Error while Generating Action:\n{e.Message}");
      return null;
    }
  }

  public IEnumerable<object> GenerateActionPhase2()
  {
    _currentPosition = (0, 0);
    _currentRotation = 0;
    IList<(int, int)> path = GetBestPath();
    int pathIndexPointer = 1;
    while (true)
    {
      Console.WriteLine(path[pathIndexPointer]);
      (int x, int y) newPos = path[pathIndexPointer];
      if (_nextActionIsMove == true)
      {
        _nextActionIsMove = false;
        _currentPosition = path[pathIndexPointer];
        pathIndexPointer++;
        yield return MoveAction();
        continue;
      }
      // find rotation needed
      int rotationNeeded = -1;
      if (newPos.x < _currentPosition.x) rotationNeeded = 270;
      else if (newPos.x > _currentPosition.x) rotationNeeded = 90;
      else if (newPos.y < _currentPosition.y) rotationNeeded = 0;
      else if (newPos.y > _currentPosition.y) rotationNeeded = 180;
      // if we have right rotation atm, move. else rotate now and move next round
      if (_currentRotation == rotationNeeded)
      {
        _currentPosition = newPos;
        pathIndexPointer++;
        yield return MoveAction();
      }
      else
      {
        _nextActionIsMove = true;
        _currentRotation = rotationNeeded;
        yield return RotateAction(rotationNeeded);
      }
    }
  }

  private object CreateNeededAction((int x, int y) pos, (int x, int y) newPos,
    int currentRotation)
  {
    // find rotation needed
    int rotationNeeded = -1;
    if (newPos.x < pos.x) rotationNeeded = 270;
    else if (newPos.x > pos.x) rotationNeeded = 90;
    else if (newPos.y < pos.y) rotationNeeded = 0;
    else if (newPos.y > pos.y) rotationNeeded = 180;
    // if we have right rotation atm, move. else rotate now and move next round
    if (currentRotation == rotationNeeded)
    {
      _currentPosition = newPos;
      return MoveAction();
    }
    else
    {
      _nextActionIsMove = true;
      _currentRotation = rotationNeeded;
      return RotateAction(rotationNeeded);
    }
  }

  // This can be used to play by yourself
  public object GetManualActionFromUser()
  {
    Console.WriteLine("\nGive action. R45 = rotate 45, M = move");
    do
    {
      string input = Console.ReadLine();
      string regexPattern = @"^R\d{2,3}$";
      if (input == "M") return MoveAction();
      // TODO check that rotation amount is allowed too. e.g. 47 is not, 45 is
      else if (Regex.IsMatch(input, regexPattern))
      {
        int rotation = Convert.ToInt32(input[1..]);
        return RotateAction(rotation);
      }
      else Console.WriteLine("Invalid Input.");
    }
    while (true);
  }

  public class RecursionKillerException : Exception
  {
    public RecursionKillerException()
    {
    }
    public RecursionKillerException(string message) : base(message)
    {
    }
  }
  private IList<(int, int)> GetBestPath()
  {
    string bestPath = "";
    HashSet<(int, int)> visited = new HashSet<(int, int)>();
    void BFS((int, int) pos, string path)
    {
      path += pos;
      if (pos == _target)
      {
        bestPath = path;
        throw new RecursionKillerException("Target found, kill recursion.");
      }
      visited.Add(pos);
      if (_graph.TryGetValue(pos, out List<(int, int)> nextPositions))
      {
        foreach (var nextPos in nextPositions)
        {
          if (!visited.Contains(nextPos))
          {
            BFS(nextPos, path);
          }
        }
      }
    }
    try
    {
      BFS(pos: (0, 0), path: "");
    }
    catch (RecursionKillerException) { }
    // do not return first square of the path because we start there
    return ParseStringPath(bestPath);
  }

  private IList<(int, int)> ParseStringPath(string path)
  {
    IList<(int, int)> result = new List<(int, int)>();
    var regex = new Regex(@"\((\d+),\s*(\d+)\)");
    var tuples = regex.Matches(path);
    foreach (Match tuple in tuples)
    {
      result.Add((int.Parse(tuple.Groups[1].Value), int.Parse(tuple.Groups[2].Value)));
    }
    return result;
  }
}
