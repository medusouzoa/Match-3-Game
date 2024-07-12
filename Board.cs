namespace Client.Core
{
    public class Board : MonoBehaviour
    {
        public Level Level;

        [SerializeField] private int _height;

        [SerializeField] private int _width;

        [SerializeField] private float _fillTime;

        [SerializeField] private GameObject _background;

        [SerializeField] private CollectingPieceAnimation _collectingPieceAnimation;

        [Header("Piece Prefabs")] [SerializeField]
        private List<PiecePrefab> _piecePrefabs;

        private readonly Dictionary<PieceType, GameObject> _piecePrefabsDictionary = new();

        [Header("Initial Pieces")] [SerializeField]
        private List<PiecePosition> _initialPieces;

        private GamePiece[,] pieces;

        private bool inverse;

        private GamePiece _pressedPiece;

        private GamePiece _enteredPiece;

        private bool _gameOver;

        public bool IsGamePiecesClickable { get; private set; } = true;
        public bool IsFilling { get; private set; }

        private bool _isSwapping;

        private bool _objectDestroying;

        private int _destroyingObjectCount;

        private void Awake()
        {
            AddPrefabsToDictionary();
            Setup();
            SetPieces();
        }

        private void AddPrefabsToDictionary()
        {
            foreach (var prefab in _piecePrefabs.Where(prefab => !_piecePrefabsDictionary.ContainsKey(prefab.Type)))
            {
                _piecePrefabsDictionary.Add(prefab.Type, prefab.Prefab);
            }
        }

        private void Setup()
        {
            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    Instantiate(_background, GetWorldPosition(x, y), Quaternion.identity, transform);
                }
            }
        }

        private void SetPieces()
        {
            pieces = new GamePiece[_width, _height];

            for (int i = 0; i < _initialPieces.Count; i++)
            {
                if (_initialPieces[i].x >= 0 && _initialPieces[i].x < _width
                                             && _initialPieces[i].y >= 0 && _initialPieces[i].y < _height)
                {
                    SpawnNewPiece(_initialPieces[i].x, _initialPieces[i].y, _initialPieces[i].type);
                }
            }

            for (int x = 0; x < _width; x++)
            {
                for (int y = 0; y < _height; y++)
                {
                    if (pieces[x, y] == null)
                    {
                        SpawnNewPiece(x, y, PieceType.Empty);
                    }
                }
            }

            StartCoroutine(Fill());
        }

        public Vector2 GetWorldPosition(int x, int y)
        {
            Vector3 position = transform.position;
            return new Vector2(position.x - _width / 2f + x + 0.5f, position.y + _height / 2f - y - 0.5f);
        }

        public GamePiece SpawnNewPiece(int x, int y, PieceType type)
        {
            GameObject newPiece = Instantiate(_piecePrefabsDictionary[type], GetWorldPosition(x, y),
                Quaternion.identity, transform);

            pieces[x, y] = newPiece.GetComponent<GamePiece>();
            pieces[x, y].Init(x, y, this, type);

            return pieces[x, y];
        }

        public IEnumerator Fill(float time = 0f)
        {
            yield return new WaitForSeconds(time);

            bool needsRefill = true;
            IsFilling = true;

            while (_objectDestroying)
            {
                yield return 0;
            }

            while (needsRefill)
            {
                yield return new WaitForSeconds(_fillTime);

                while (FillStep())
                {
                    inverse = !inverse;
                    yield return new WaitForSeconds(_fillTime);
                }

                needsRefill = ClearAllValidMatches();
            }

            IsFilling = false;
        }

        public bool FillStep()
        {
            bool movedPiece = false;

            movedPiece |= MovePiecesDown();
            movedPiece |= FillTopRow();

            return movedPiece;
        }

        private bool MovePiecesDown()
        {
            bool movedPiece = false;

            for (int y = _height - 2; y >= 0; y--)
            {
                for (int loopX = 0; loopX < _width; loopX++)
                {
                    int x = inverse ? _width - 1 - loopX : loopX;
                    GamePiece piece = pieces[x, y];

                    if (!piece.IsMovable()) continue;

                    if (MovePieceDown(x, y, piece))
                    {
                        movedPiece = true;
                    }
                    else if (MovePieceDiagonally(x, y, piece))
                    {
                        movedPiece = true;
                    }
                }
            }

            return movedPiece;
        }

        private bool MovePieceDown(int x, int y, GamePiece piece)
        {
            GamePiece pieceBelow = pieces[x, y + 1];

            if (pieceBelow.PieceType != PieceType.Empty) return false;

            Destroy(pieceBelow.gameObject);
            piece.MovableComponent.Move(x, y + 1, _fillTime);
            pieces[x, y + 1] = piece;
            SpawnNewPiece(x, y, PieceType.Empty);

            return true;
        }

        private bool MovePieceDiagonally(int x, int y, GamePiece piece)
        {
            for (int diag = -1; diag <= 1; diag++)
            {
                if (diag == 0) continue;

                int diagX = inverse ? x - diag : x + diag;

                if (diagX < 0 || diagX >= _width) continue;

                GamePiece diagonalPiece = pieces[diagX, y + 1];

                if (diagonalPiece.PieceType != PieceType.Empty || !HasSpaceAbove(diagX, y)) continue;

                Destroy(diagonalPiece.gameObject);
                piece.MovableComponent.Move(diagX, y + 1, _fillTime);
                pieces[diagX, y + 1] = piece;
                SpawnNewPiece(x, y, PieceType.Empty);

                return true;
            }

            return false;
        }

        private bool HasSpaceAbove(int x, int y)
        {
            for (int aboveY = y; aboveY >= 0; aboveY--)
            {
                GamePiece pieceAbove = pieces[x, aboveY];

                if (pieceAbove.IsMovable())
                {
                    return true;
                }

                if (!pieceAbove.IsMovable() && pieceAbove.PieceType != PieceType.Empty)
                {
                    return false;
                }
            }

            return true;
        }

        private bool FillTopRow()
        {
            bool movedPiece = false;

            for (int x = 0; x < _width; x++)
            {
                GamePiece pieceBelow = pieces[x, 0];

                if (pieceBelow.PieceType != PieceType.Empty) continue;

                Destroy(pieceBelow.gameObject);
                GamePiece newPiece = CreateNewPiece(x, -1, PieceType.Normal);
                newPiece.MovableComponent.Move(x, 0, _fillTime);
                pieces[x, 0] = newPiece;
                movedPiece = true;
            }

            return movedPiece;
        }

        private GamePiece CreateNewPiece(int x, int y, PieceType type)
        {
            GameObject newPieceObject = Instantiate(_piecePrefabsDictionary[type], GetWorldPosition(x, y),
                Quaternion.identity, transform);
            GamePiece newPiece = newPieceObject.GetComponent<GamePiece>();
            newPiece.Init(x, y, this, type);
            newPiece.ColorComponent.SetColor((ColorType)Random.Range(0, newPiece.ColorComponent.ColorNumber));
            return newPiece;
        }


        public bool IsAdjacent(GamePiece piece1, GamePiece piece2)
        {
            return (piece1.X == piece2.X && Mathf.Abs(piece1.Y - piece2.Y) == 1)
                   || (piece1.Y == piece2.Y && Mathf.Abs(piece1.X - piece2.X) == 1);
        }

        private const float _swapPieceTime = 0.15f;

        public void SwapPieces(GamePiece piece1, GamePiece piece2)
        {
            if (_gameOver || IsFilling || _isSwapping || !piece1.IsMovable() || !piece2.IsMovable())
            {
                return;
            }

            SwapPiecePositions(piece1, piece2);

            if (IsValidSwap(piece1, piece2))
            {
                ExecuteSwap(piece1, piece2);
            }
            else
            {
                StartCoroutine(InvalidSwap(piece1, piece2));
            }
        }

        private void SwapPiecePositions(GamePiece piece1, GamePiece piece2)
        {
            pieces[piece1.X, piece1.Y] = piece2;
            pieces[piece2.X, piece2.Y] = piece1;
        }

        private bool IsValidSwap(GamePiece piece1, GamePiece piece2)
        {
            return GetMatch(piece1, piece2.X, piece2.Y) != null || GetMatch(piece2, piece1.X, piece1.Y) != null ||
                   IsSpecialPiece(piece1) || IsSpecialPiece(piece2);
        }

        private bool IsSpecialPiece(GamePiece piece)
        {
            return piece.PieceType == PieceType.Rainbow || piece.PieceType == PieceType.ColumnClear ||
                   piece.PieceType == PieceType.RowClear || piece.PieceType == PieceType.Bomb;
        }

        private void ExecuteSwap(GamePiece piece1, GamePiece piece2)
        {
            _isSwapping = true;

            int piece1X = piece1.X;
            int piece1Y = piece1.Y;

            piece1.MovableComponent.Move(piece2.X, piece2.Y, _swapPieceTime);
            piece2.MovableComponent.Move(piece1X, piece1Y, _swapPieceTime);

            MatchKey matchKey = DetermineMatchKey(piece1, piece2);

            HandleSpecialPieces(piece1, piece2, matchKey);

            ClearAllValidMatches();

            _enteredPiece = null;
            _pressedPiece = null;

            StartCoroutine(Fill());

            Level.OnMove();

            _isSwapping = false;
        }

        private MatchKey DetermineMatchKey(GamePiece piece1, GamePiece piece2)
        {
            if (piece1.PieceType == PieceType.Rainbow && piece1.IsClearable() && piece2.IsColored())
            {
                return MatchKey.Rainbow;
            }

            if (piece2.PieceType == PieceType.Rainbow && piece2.IsClearable() && piece1.IsColored())
            {
                return MatchKey.Rainbow;
            }

            if (piece1.PieceType == PieceType.RowClear || piece1.PieceType == PieceType.ColumnClear)
            {
                return MatchKey.Column;
            }

            if (piece2.PieceType == PieceType.RowClear || piece2.PieceType == PieceType.ColumnClear)
            {
                return MatchKey.Row;
            }

            if (piece1.PieceType == PieceType.Bomb)
            {
                return MatchKey.Bomb;
            }

            if (piece2.PieceType == PieceType.Bomb)
            {
                return MatchKey.Bomb;
            }

            return MatchKey.Empty;
        }

        private void HandleSpecialPieces(GamePiece piece1, GamePiece piece2, MatchKey matchKey)
        {
            switch (matchKey)
            {
                case MatchKey.Rainbow:
                    if (piece1.PieceType == PieceType.Rainbow)
                    {
                        RainbowSuper(piece1, piece2);
                    }
                    else
                    {
                        RainbowSuper(piece2, piece1);
                    }

                    break;
                case MatchKey.Column:
                    RocketSuper(piece1, piece2);
                    break;
                case MatchKey.Row:
                    RocketSuper(piece2, piece1);
                    break;
                case MatchKey.Bomb:
                    Bomb(piece1, piece2);
                    break;
            }
        }

        private IEnumerator InvalidSwap(GamePiece piece1, GamePiece piece2)
        {
            _isSwapping = true;

            int piece1X = piece1.X;
            int piece1Y = piece1.Y;

            int piece2X = piece2.X;
            int piece2Y = piece2.Y;

            piece1.MovableComponent.Move(piece2X, piece2Y, _swapPieceTime);
            piece2.MovableComponent.Move(piece1X, piece1Y, _swapPieceTime);

            yield return new WaitForSeconds(0.3f);

            piece1.MovableComponent.Move(piece1X, piece1Y, _swapPieceTime);
            piece2.MovableComponent.Move(piece2X, piece2Y, _swapPieceTime);

            pieces[piece1.X, piece1.Y] = piece1;
            pieces[piece2.X, piece2.Y] = piece2;

            _isSwapping = false;
        }

        public void PressPiece(GamePiece piece)
        {
            _pressedPiece = piece;
        }

        public void EnterPiece(GamePiece piece)
        {
            _enteredPiece = piece;
        }

        public void ReleasePiece()
        {
            if (_pressedPiece == null || _enteredPiece == null)
                return;

            if (IsAdjacent(_pressedPiece, _enteredPiece))
            {
                SwapPieces(_pressedPiece, _enteredPiece);
            }
        }

        public List<GamePiece> GetMatch(GamePiece piece, int newX, int newY)
        {
            if (!piece.IsColored()) return null;

            List<GamePiece> matchingPieces = new();
            ColorType color = piece.ColorComponent.Color;

            List<GamePiece> horizontalPieces = GetHorizontalMatch(piece, newX, newY, color);
            if (horizontalPieces.Count >= 3)
            {
                matchingPieces.AddRange(horizontalPieces);
                List<GamePiece> verticalPiecesFromHorizontal =
                    GetVerticalMatchFromHorizontal(horizontalPieces, newY, color);
                if (verticalPiecesFromHorizontal.Count >= 2)
                {
                    matchingPieces.AddRange(verticalPiecesFromHorizontal);
                }
            }

            List<GamePiece> verticalPieces = GetVerticalMatch(piece, newX, newY, color);
            if (verticalPieces.Count >= 3)
            {
                matchingPieces.AddRange(verticalPieces);
                List<GamePiece> horizontalPiecesFromVertical =
                    GetHorizontalMatchFromVertical(verticalPieces, newX, color);
                if (horizontalPiecesFromVertical.Count >= 2)
                {
                    matchingPieces.AddRange(horizontalPiecesFromVertical);
                }
            }

            return matchingPieces.Count >= 3 ? matchingPieces : null;
        }

        private List<GamePiece> GetHorizontalMatch(GamePiece piece, int newX, int newY, ColorType color)
        {
            List<GamePiece> horizontalPieces = new() { piece };

            for (int dir = -1; dir <= 1; dir += 2)
            {
                for (int xOffset = 1; xOffset < _width; xOffset++)
                {
                    int x = newX + dir * xOffset;
                    if (x < 0 || x >= _width) break;

                    if (pieces[x, newY].IsColored() && pieces[x, newY].ColorComponent.Color == color &&
                        pieces[x, newY].PieceType == PieceType.Normal)
                    {
                        horizontalPieces.Add(pieces[x, newY]);
                    }
                    else
                    {
                        break;
                    }
                }
            }

            return horizontalPieces;
        }

        private List<GamePiece> GetVerticalMatch(GamePiece piece, int newX, int newY, ColorType color)
        {
            List<GamePiece> verticalPieces = new() { piece };

            for (int dir = -1; dir <= 1; dir += 2)
            {
                for (int yOffset = 1; yOffset < _height; yOffset++)
                {
                    int y = newY + dir * yOffset;
                    if (y < 0 || y >= _height) break;

                    if (pieces[newX, y].IsColored() && pieces[newX, y].ColorComponent.Color == color &&
                        pieces[newX, y].PieceType == PieceType.Normal)
                    {
                        verticalPieces.Add(pieces[newX, y]);
                    }
                    else
                    {
                        break;
                    }
                }
            }

            return verticalPieces;
        }

        private List<GamePiece> GetVerticalMatchFromHorizontal(List<GamePiece> horizontalPieces, int newY,
            ColorType color)
        {
            foreach (GamePiece piece in horizontalPieces)
            {
                List<GamePiece> verticalPieces = GetVerticalPiecesFromPiece(piece, newY, color);

                if (verticalPieces.Count >= 2)
                {
                    return verticalPieces;
                }
            }

            return new List<GamePiece>();
        }

        private List<GamePiece> GetVerticalPiecesFromPiece(GamePiece piece, int startY, ColorType color)
        {
            List<GamePiece> verticalPieces = new();

            for (int dir = -1; dir <= 1; dir += 2)
            {
                for (int yOffset = 1; yOffset < _height; yOffset++)
                {
                    int y = startY + dir * yOffset;
                    if (y < 0 || y >= _height) break;

                    if (IsMatchingPiece(pieces[piece.X, y], color))
                    {
                        verticalPieces.Add(pieces[piece.X, y]);
                    }
                    else
                    {
                        break;
                    }
                }
            }

            return verticalPieces;
        }

        private List<GamePiece> GetHorizontalMatchFromVertical
            (List<GamePiece> verticalPieces, int newX, ColorType color)
        {
            foreach (GamePiece piece in verticalPieces)
            {
                List<GamePiece> horizontalPieces = GetHorizontalPiecesFromPiece(piece, newX, color);

                if (horizontalPieces.Count >= 2)
                {
                    return horizontalPieces;
                }
            }

            return new List<GamePiece>();
        }

        private List<GamePiece> GetHorizontalPiecesFromPiece
            (GamePiece piece, int startX, ColorType color)
        {
            List<GamePiece> horizontalPieces = new();

            for (int dir = -1; dir <= 1; dir += 2)
            {
                for (int xOffset = 1; xOffset < _width; xOffset++)
                {
                    int x = startX + dir * xOffset;
                    if (x < 0 || x >= _width) break;

                    if (IsMatchingPiece(pieces[x, piece.Y], color))
                    {
                        horizontalPieces.Add(pieces[x, piece.Y]);
                    }
                    else
                    {
                        break;
                    }
                }
            }

            return horizontalPieces;
        }

        private bool IsMatchingPiece(GamePiece piece, ColorType color)
        {
            return piece.IsColored() && piece.ColorComponent.Color ==
                color && piece.PieceType == PieceType.Normal;
        }

        public bool ClearAllValidMatches()
        {
            bool needsRefill = false;

            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    if (!pieces[x, y].IsClearable()) continue;

                    List<GamePiece> match = GetMatch(pieces[x, y], x, y);
                    if (match == null) continue;

                    SetObjectDestroying(true);
                    needsRefill = ProcessMatch(match, ref needsRefill);
                }
            }

            return needsRefill;
        }

        private bool ProcessMatch(List<GamePiece> match, ref bool needsRefill)
        {
            PieceType specialPieceType = GetSpecialPieceType(match);
            (int specialPieceX, int specialPieceY) = GetSpecialPieceCoordinates(match);

            foreach (GamePiece piece in match)
            {
                if (ClearPiece(piece.X, piece.Y))
                {
                    needsRefill = true;
                }
            }

            if (specialPieceType != PieceType.Count)
            {
                CreateSpecialPiece(specialPieceType, specialPieceX, specialPieceY, match);
            }

            return needsRefill;
        }

        private PieceType GetSpecialPieceType(List<GamePiece> match)
        {
            if (match.Count == 4)
            {
                return GetFourMatchSpecialPieceType();
            }
            else if (match.Count >= 5)
            {
                return CheckRainbowOrBomb(match);
            }

            return PieceType.Count;
        }

        private PieceType GetFourMatchSpecialPieceType()
        {
            if (_pressedPiece == null || _enteredPiece == null)
            {
                return (PieceType)Random.Range((int)PieceType.RowClear, (int)PieceType.ColumnClear);
            }
            else if (_pressedPiece.Y == _enteredPiece.Y)
            {
                return PieceType.RowClear;
            }
            else
            {
                return PieceType.ColumnClear;
            }
        }

        private (int, int) GetSpecialPieceCoordinates(List<GamePiece> match)
        {
            GamePiece randomPiece = match[Random.Range(0, match.Count)];
            int specialPieceX = randomPiece.X;
            int specialPieceY = randomPiece.Y;

            foreach (GamePiece piece in match)
            {
                if (piece == _pressedPiece || piece == _enteredPiece)
                {
                    specialPieceX = piece.X;
                    specialPieceY = piece.Y;
                }
            }

            return (specialPieceX, specialPieceY);
        }

        private void CreateSpecialPiece(PieceType specialPieceType, int x, int y, List<GamePiece> match)
        {
            Destroy(pieces[x, y]);
            GamePiece newPiece = SpawnNewPiece(x, y, specialPieceType);

            if (newPiece.IsColored() && match[0].IsColored())
            {
                switch (specialPieceType)
                {
                    case PieceType.RowClear:
                        newPiece.SetPieceTypeInitial(PieceType.RowClear, ColorType.Any);
                        break;
                    case PieceType.ColumnClear:
                        newPiece.SetPieceTypeInitial(PieceType.ColumnClear, ColorType.Any);
                        break;
                    case PieceType.Rainbow:
                        newPiece.SetPieceTypeInitial(PieceType.Rainbow, ColorType.Any);
                        break;
                    case PieceType.Bomb:
                        newPiece.SetPieceTypeInitial(PieceType.Bomb, ColorType.Any);
                        break;
                }
            }
        }

        public PieceType CheckRainbowOrBomb(List<GamePiece> matches)
        {
            int xCount = 0;
            int yCount = 0;

            for (int i = 0; i < matches.Count - 1; i++)
            {
                if (matches[i].X == matches[i + 1].X)
                {
                    xCount++;
                }

                if (matches[i].Y == matches[i + 1].Y)
                {
                    yCount++;
                }
            }

            if (xCount >= 4 || yCount >= 4)
            {
                return PieceType.Rainbow;
            }

            return PieceType.Bomb;
        }

        public bool ClearPiece(int x, int y)
        {
            GamePiece piece = pieces[x, y];

            if (!piece.IsClearable() || piece.ClearableComponent.IsBeingCleared) return false;

            piece.ClearableComponent.Clear();
            Level.OnPieceCleared(piece);
            SpawnNewPiece(x, y, PieceType.Empty);
            ClearObstacles(x, y);

            return true;
        }

        public IEnumerator StartDestroyAnimation(GamePiece piece, int score)
        {
            yield return new WaitForSeconds(_swapPieceTime);

            Vector3 objectPosition = piece.transform.position;
            _collectingPieceAnimation.AddObjects(objectPosition, score);
        }

        public void ClearObstacles(int x, int y)
        {
            ClearHorizontalObstacles(x, y);
            ClearVerticalObstacles(x, y);
        }

        private void ClearHorizontalObstacles(int x, int y)
        {
            for (int adjacentX = x - 1; adjacentX <= x + 1; adjacentX++)
            {
                if (adjacentX == x || !IsWithinWidthBounds(adjacentX)) continue;

                ClearPieceIfObstacle(adjacentX, y);
            }
        }

        private void ClearVerticalObstacles(int x, int y)
        {
            for (int adjacentY = y - 1; adjacentY <= y + 1; adjacentY++)
            {
                if (adjacentY == y || !IsWithinHeightBounds(adjacentY)) continue;

                ClearPieceIfObstacle(x, adjacentY);
            }
        }

        private bool IsWithinWidthBounds(int x)
        {
            return x >= 0 && x < _width;
        }

        private bool IsWithinHeightBounds(int y)
        {
            return y >= 0 && y < _height;
        }

        private void ClearPieceIfObstacle(int x, int y)
        {
            GamePiece piece = pieces[x, y];
            if (piece.PieceType == PieceType.Obstacle && piece.IsClearable())
            {
                bool isCleared = piece.ClearableComponent.Clear();
                if (isCleared)
                {
                    SpawnNewPiece(x, y, PieceType.Empty);
                }
            }
        }

        #region Rocket

        [SerializeField] private Transform _rocketPool;

        private readonly Queue<GameObject> objectPoolQueue = new();

        private const float _rocketDisableTime = 1.5f;

        private const int _waitPieceDestroying = 80;

        private const int _waitToFill = 200;

        public async Task RowRocket(GameObject halfRocket, int x, int y, PieceType pieceType)
        {
            GetRocketFromPool(out GameObject leftRocket, out GameObject rightRocket, halfRocket);

            SetHalfRocket(leftRocket, new Vector2(x - 1, y), Quaternion.Euler(0, 0, 90), "Left Rocket");
            SetHalfRocket(rightRocket, new Vector2(x + 1, y), Quaternion.Euler(0, 0, 270), "Right Rocket");

            Task task1 = RightClearRowRocket(x, y);
            Task task2 = LeftClearRowRocket(x, y);

            await Task.WhenAll(task1, task2);
            await Task.Delay(_waitToFill);

            FinishDestroyingObjectCallers(0f, pieceType);

            Fillers();
        }

        public async Task ColumnRocket(GameObject halfRocket, int x, int y, PieceType pieceType)
        {
            GetRocketFromPool(out GameObject upRocket, out GameObject downRocket, halfRocket);

            SetHalfRocket(upRocket, new Vector2(x, y + 1), Quaternion.Euler(0, 0, 180), "Up Rocket");
            SetHalfRocket(downRocket, new Vector2(x, y - 1), Quaternion.Euler(0, 0, 0), "Down Rocket");

            Task task1 = BottomClearColumnRocket(x, y);
            Task task2 = UpperClearColumnRocket(x, y);

            await Task.WhenAll(task1, task2);
            await Task.Delay(_waitToFill);

            FinishDestroyingObjectCallers(0f, pieceType);

            Fillers();
        }

        private void SetHalfRocket(GameObject rocket, Vector2 pos, Quaternion rotation, string newName)
        {
            rocket.transform.position = GetWorldPosition((int)pos.x, (int)pos.y);
            rocket.transform.localRotation = rotation;
            rocket.name = newName;
            rocket.SetActive(true);
        }

        private void GetRocketFromPool(out GameObject rocket1, out GameObject rocket2, GameObject halfRocket)
        {
            if (objectPoolQueue.Count > 1)
            {
                rocket1 = objectPoolQueue.Dequeue();
                rocket2 = objectPoolQueue.Dequeue();

                StartCoroutine(AddToPool(_rocketDisableTime, rocket1));
                StartCoroutine(AddToPool(_rocketDisableTime, rocket2));
            }
            else
            {
                rocket1 = CreateHalfRocket(halfRocket);
                rocket2 = CreateHalfRocket(halfRocket);
            }
        }

        private GameObject CreateHalfRocket(GameObject halfRocket)
        {
            GameObject newRocketObject = Instantiate(halfRocket, Vector3.zero, Quaternion.identity, _rocketPool);
            newRocketObject.SetActive(false);
            newRocketObject.GetComponent<Rocket>().SetBoard(this);

            StartCoroutine(AddToPool(_rocketDisableTime, newRocketObject));

            return newRocketObject;
        }

        private IEnumerator AddToPool(float time, GameObject rocket)
        {
            yield return new WaitForSeconds(time);
            objectPoolQueue.Enqueue(rocket);
        }

        public void RocketSuper(GamePiece rocketPiece, GamePiece anotherPiece)
        {
            SetObjectDestroying(true);

            if (IsRowOrColumnClearPiece(anotherPiece))
            {
                HandleRowOrColumnClearInteraction(rocketPiece, anotherPiece);
            }
            else if (anotherPiece.PieceType == PieceType.Bomb)
            {
                HandleBombInteraction(rocketPiece);
            }
            else if (anotherPiece.IsClearable() && anotherPiece.PieceType == PieceType.Normal)
            {
                rocketPiece.ClearableComponent.Clear();
            }
        }

        private bool IsRowOrColumnClearPiece(GamePiece piece)
        {
            return piece.PieceType == PieceType.ColumnClear || piece.PieceType == PieceType.RowClear;
        }

        private void HandleRowOrColumnClearInteraction(GamePiece rocketPiece, GamePiece anotherPiece)
        {
            if (rocketPiece.X == anotherPiece.X)
            {
                anotherPiece.SetPieceTypeInitial(PieceType.ColumnClear, ColorType.Any);
                rocketPiece.SetPieceTypeInitial(PieceType.RowClear, ColorType.Any);
            }
            else if (rocketPiece.Y == anotherPiece.Y)
            {
                anotherPiece.SetPieceTypeInitial(PieceType.RowClear, ColorType.Any);
                rocketPiece.SetPieceTypeInitial(PieceType.ColumnClear, ColorType.Any);
            }

            UpdatePiecePositions(rocketPiece, anotherPiece);
            ClearPieces(rocketPiece, anotherPiece);
        }

        private void UpdatePiecePositions(GamePiece rocketPiece, GamePiece anotherPiece)
        {
            rocketPiece.X = _pressedPiece.X;
            rocketPiece.Y = _pressedPiece.Y;
            anotherPiece.X = _pressedPiece.X;
            anotherPiece.Y = _pressedPiece.Y;
        }

        private void ClearPieces(GamePiece rocketPiece, GamePiece anotherPiece)
        {
            rocketPiece.ClearableComponent.Clear();
            anotherPiece.ClearableComponent.Clear();
        }

        private void HandleBombInteraction(GamePiece rocketPiece)
        {
            GamePiece superRocketPiece = DestroyAndCreateNewPiece(_pressedPiece, _pressedPiece.X, _pressedPiece.Y,
                PieceType.SuperRocket, ColorType.Any);
            superRocketPiece.ClearableComponent.Clear();

            ClearAdjacentPieces(_pressedPiece.X, _pressedPiece.Y, PieceType.ColumnClear, _width, true);
            ClearAdjacentPieces(_pressedPiece.Y, _pressedPiece.X, PieceType.RowClear, _height, false);
        }

        private void ClearAdjacentPieces(int mainCoordinate, int fixedCoordinate, PieceType pieceType, int boundary,
            bool isHorizontal)
        {
            for (int i = mainCoordinate - 1; i <= mainCoordinate + 1; i++)
            {
                if (i == mainCoordinate || i < 0 || i >= boundary) continue;

                GamePiece piece = isHorizontal
                    ? DestroyAndCreateNewPiece(pieces[i, fixedCoordinate], i, fixedCoordinate, pieceType, ColorType.Any)
                    : DestroyAndCreateNewPiece(pieces[fixedCoordinate, i], fixedCoordinate, i, pieceType,
                        ColorType.Any);

                piece.ClearableComponent.Clear();
            }
        }

        public async Task RightClearRowRocket(int x, int y)
        {
            for (int i = x; i >= 0; i--)
            {
                ClearPiece(i, y);

                await Task.Delay(_waitPieceDestroying);
            }
        }

        public async Task LeftClearRowRocket(int x, int y)
        {
            for (int i = x; i < _width; i++)
            {
                ClearPiece(i, y);

                await Task.Delay(_waitPieceDestroying);
            }
        }

        public async Task BottomClearColumnRocket(int x, int y)
        {
            for (int i = y; i < _height; i++)
            {
                ClearPiece(x, i);

                await Task.Delay(_waitPieceDestroying);
            }
        }

        public async Task UpperClearColumnRocket(int x, int y)
        {
            for (int i = y; i >= 0; i--)
            {
                ClearPiece(x, i);

                await Task.Delay(_waitPieceDestroying);
            }
        }

        #endregion


        private const int ChanceOfCreatingSpecialObjectByRainbow = 20;

        public void RainbowSuper(GamePiece rainbowPiece, GamePiece anotherPiece)
        {
            SetObjectDestroying(true);

            rainbowPiece.GetComponent<RainbowPiece>().SetPieces(anotherPiece);

            rainbowPiece.ClearableComponent.Clear();
        }

        public async Task ClearRainbow(GamePiece rainbowPiece, GamePiece anotherPiece, ColorType colorType)
        {
            await ClearRainbowTask(rainbowPiece, anotherPiece, colorType);
            await Task.Delay(_waitToFill);

            FinishDestroyingObjectCallers(0f, PieceType.Rainbow);

            Fillers();
        }

        private async Task ClearRainbowTask(GamePiece rainbowPiece, GamePiece anotherPiece, ColorType colorType)
        {
            for (int x = 0; x < _width; x++)
            {
                for (int y = 0; y < _height; y++)
                {
                    ClearIfRainbowPiece(x, y, rainbowPiece);
                    await ClearByColorType(x, y, colorType);
                    await ClearIfSpecialPiece(x, y, anotherPiece);
                }
            }
        }

        private void ClearIfRainbowPiece(int x, int y, GamePiece rainbowPiece)
        {
            if (x == rainbowPiece.X && y == rainbowPiece.Y)
            {
                ClearPiece(x, y);
            }
        }

        private Task ClearByColorType(int x, int y, ColorType colorType)
        {
            if (colorType != ColorType.Any && pieces[x, y].IsColored() &&
                pieces[x, y].ColorComponent.Color == colorType)
            {
                ClearPiece(x, y);
            }

            return Task.CompletedTask;
        }

        private async Task ClearIfSpecialPiece(int x, int y, GamePiece anotherPiece)
        {
            if (anotherPiece.PieceType == PieceType.Rainbow)
            {
                ClearPiece(x, y);
            }
            else if (anotherPiece.PieceType == PieceType.Bomb)
            {
                await HandleBombPiece(x, y, anotherPiece);
            }
            else if (anotherPiece.PieceType == PieceType.RowClear || anotherPiece.PieceType == PieceType.ColumnClear)
            {
                await HandleRowOrColumnClearPiece(x, y, anotherPiece);
            }
            else if (pieces[x, y].IsColored() && pieces[x, y].ColorComponent.Color == anotherPiece.ColorComponent.Color)
            {
                ClearPiece(x, y);
            }
        }

        private Task HandleBombPiece(int x, int y, GamePiece anotherPiece)
        {
            if (x == anotherPiece.X && y == anotherPiece.Y)
            {
                anotherPiece.ClearableComponent.Clear();
            }

            if (Random.Range(0, 101) < ChanceOfCreatingSpecialObjectByRainbow &&
                pieces[x, y].PieceType == PieceType.Normal)
            {
                GamePiece gamePiece = DestroyAndCreateNewPiece(pieces[x, y], x, y, PieceType.Bomb, ColorType.Any);
                gamePiece.ClearableComponent.Clear();
            }

            return Task.CompletedTask;
        }

        private Task HandleRowOrColumnClearPiece(int x, int y, GamePiece anotherPiece)
        {
            if (x == anotherPiece.X && y == anotherPiece.Y)
            {
                anotherPiece.ClearableComponent.Clear();
            }

            if (Random.Range(0, 101) < ChanceOfCreatingSpecialObjectByRainbow &&
                pieces[x, y].PieceType == PieceType.Normal)
            {
                PieceType newPieceType = Random.Range(0, 2) == 0 ? PieceType.RowClear : PieceType.ColumnClear;
                GamePiece gamePiece = DestroyAndCreateNewPiece(pieces[x, y], x, y, newPieceType, ColorType.Any);
                gamePiece.ClearableComponent.Clear();
            }

            return Task.CompletedTask;
        }


        public void Bomb(GamePiece bombPiece, GamePiece anotherPiece)
        {
            if (anotherPiece.PieceType == PieceType.Normal)
            {
                bombPiece.ClearableComponent.Clear();
            }
            else if (anotherPiece.PieceType == PieceType.Bomb)
            {
                GamePiece superRocketPiece = DestroyAndCreateNewPiece(bombPiece,
                    bombPiece.X, bombPiece.Y, PieceType.SuperBomb, ColorType.Any);
                superRocketPiece.ClearableComponent.Clear();
            }
        }

        public async Task ClearBomb(GamePiece bombPiece, int radius = 1)
        {
            await ClearBombTask(bombPiece, radius);
            await Task.Delay(_waitToFill * 4);

            FinishDestroyingObjectCallers(0f, PieceType.Bomb);

            Fillers();
        }

        private Task ClearBombTask(GamePiece bombPiece, int radius = 1)
        {
            for (int adjacentX = bombPiece.X - radius; adjacentX <= bombPiece.X + radius; adjacentX++)
            {
                if (adjacentX < 0 || adjacentX >= _width) continue;

                for (int adjacentY = bombPiece.Y - radius; adjacentY <= bombPiece.Y + radius; adjacentY++)
                {
                    if (adjacentY < 0 || adjacentY >= _height) continue;
                    ClearPiece(adjacentX, adjacentY);
                }
            }

            return Task.CompletedTask;
        }

        private GamePiece DestroyAndCreateNewPiece(GamePiece gamePiece, int x, int y, PieceType pieceType,
            ColorType colorType)
        {
            Destroy(gamePiece.gameObject);
            GamePiece newPiece = SpawnNewPiece(x, y, pieceType);
            pieces[x, y] = newPiece;
            pieces[x, y].SetPieceTypeInitial(pieceType, colorType);

            return pieces[x, y];
        }

        public void SetPieceClickable(bool value)
        {
            IsGamePiecesClickable = value;
        }

        private const float DestroyingObjectTime = 0.0f;

        public void FinishDestroyingObjectCallers(float time =
            DestroyingObjectTime, PieceType pieceType = PieceType.Normal)
        {
            if (_destroyingObjectCount > 0)
            {
                if (pieceType == PieceType.Normal) return;

                _destroyingObjectCount--;

                if (_destroyingObjectCount > 0) return;
            }

            StopCoroutine(FinishDestroyingObject(time));
            StartCoroutine(FinishDestroyingObject(time));
        }

        private IEnumerator FinishDestroyingObject(float time)
        {
            yield return new WaitForSeconds(time);

            _objectDestroying = false;
        }

        public void IncreaseDestroyingObjectCount()
        {
            _destroyingObjectCount++;
        }

        public int GetObjectDestroyingCount()
        {
            return _destroyingObjectCount;
        }

        public void SetObjectDestroying(bool value)
        {
            _objectDestroying = value;
        }

        private const float StandardFillTime = 0.15f;

        public void Fillers(float newFillTime = StandardFillTime)
        {
            ChangeFillTime(newFillTime);

            ClearAllValidMatches();

            StartCoroutine(Fill());
        }

        private void ChangeFillTime(float newFillTime = StandardFillTime)
        {
            _fillTime = newFillTime;
        }

        public void GameOver()
        {
            _gameOver = true;
        }

        public List<GamePiece> GetTypeOfPieces(PieceType type)
        {
            List<GamePiece> gamePieces = new();

            for (int x = 0; x < _width; x++)
            {
                for (int y = 0; y < _height; y++)
                {
                    if (pieces[x, y].PieceType == type)
                    {
                        gamePieces.Add(pieces[x, y]);
                    }
                }
            }

            return gamePieces;
        }

        public List<PiecePosition> GetInitialPieces(PieceType type)
        {
            List<PiecePosition> gamePieces = new();

            for (int i = 0; i < _initialPieces.Count; i++)
            {
                if (_initialPieces[i].type == type)
                {
                    gamePieces.Add(_initialPieces[i]);
                }
            }

            return gamePieces;
        }

        #region Skills

        public SkillKey SkillKey { get; private set; } = SkillKey.Empty;

        public SkillPanel skillPanel;

        public void SetSkillType(SkillKey skillKey)
        {
            SkillKey = skillKey;
        }

        public void PaintPiece(GamePiece piece)
        {
            piece.ColorComponent.SetColor(skillPanel.GetColorType());
            Fillers();

            skillPanel.DecreaseSkillCount(SkillKey.Paint);
            SetSkillType(SkillKey.Empty);
        }

        public void BreakPiece(int x, int y)
        {
            skillPanel.DecreaseSkillCount(SkillKey.Break);

            SetObjectDestroying(true);
            ClearPiece(x, y);
            Fillers();
            SetSkillType(SkillKey.Empty);
        }

        #endregion
    }
}
