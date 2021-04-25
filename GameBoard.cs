using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MusicPlayer;
using System;
using System.Collections.Generic;

namespace PuzzleBattle
{
    public class GameBoard : Game
    {
        GraphicsDeviceManager graphics;
        SpriteBatch spriteBatch;

        BlendState alphaBlend = new BlendState()
        {
            AlphaBlendFunction = BlendFunction.Add,
            AlphaSourceBlend = Blend.One,
            AlphaDestinationBlend = Blend.InverseSourceAlpha,

            ColorBlendFunction = BlendFunction.Add,
            ColorSourceBlend = Blend.SourceAlpha,
            ColorDestinationBlend = Blend.InverseSourceAlpha
        };

        Random random;

        GameState CurrentGameState = GameState.Match;
        MatchState CurrentMatchState;

        // 全域遊戲狀態紀錄區
        enum GameState
        {
            StartScreen,
            Match,
            ResultScreen
        }
        enum MatchPhase
        {
            NotStarted,
            TurnStart,
            Placement,
            AITurnStart,
            AIPlacement,
            AfterPlacement,
            Ended
        }

        class MatchState
        {
            // TODO：PVP模式
            public bool VSMode = false;

            public int Turns = 1;
            // public TimeSpan LastLoopStartTime;
            public MatchPhase Phase = MatchPhase.NotStarted;
            public double AIActionTimer = 0;
            public MovementState PlacementAnimation = new MovementState(0, 0, 0);
            public bool IsPointingTile = false;
            public short PointedTileX, PointedTileY;

            public class PlayerState
            {
                // 存放Reserves與Next Piece的陣列
                public PieceShape[] Reserves = new PieceShape[] { new PieceShape(), new PieceShape(), new PieceShape() };
                public PieceShape NextPiece;

                // 集氣條
                public short BombGauge = 0, DisplayedBombGauge = 0;

                // 玩家動畫
                public enum AnimationState
                {
                    Idle,       // 一般狀態
                    // TODO：Panic（可下的步數很少時）
                    Fall,       // 確定落敗時（單次動畫）
                    Dead,       // 落敗後（靜止）
                    Victory     // 勝利時（循環動畫）
                }
                AnimationState _aniState;
                short _aFrame = 0;
                public AnimationState AvatarState
                {
                    get
                    {
                        return _aniState;
                    }
                    set
                    {
                        // 每次改變State都從頭開始播
                        _aniState = value;
                        _aFrame = 0;
                    }
                }

                public short AvatarCurrentFrame
                {
                    get
                    {
                        return (short)(Math.Floor(_aFrame / (60f / AVATAR_FRAMERATE)) + 1);
                    }
                    set
                    {
                        _aFrame = (short)(value * (int)(60f / AVATAR_FRAMERATE) - 1);
                    }
                }

                /// <summary>
                /// 推進Avatar動畫
                /// </summary>
                public void AvatarFrameForward()
                {
                    _aFrame++;
                    switch (AvatarState)
                    {
                        case AnimationState.Idle:
                            if (AvatarCurrentFrame > AVATAR_IDLE_FRAMES) _aFrame = 0;
                            break;
                        case AnimationState.Fall:
                            if (AvatarCurrentFrame > AVATAR_FALL_FRAMES)
                            {
                                _aFrame = 0;
                                AvatarState = AnimationState.Dead;
                            }
                            break;
                        case AnimationState.Dead:
                            if (AvatarCurrentFrame > AVATAR_DEAD_FRAMES) _aFrame = 0;
                            break;
                        case AnimationState.Victory:
                            if (AvatarCurrentFrame > AVATAR_VICTORY_FRAMES) _aFrame = 0;
                            break;
                    }
                }

            }
            public List<PlayerState> Player = new List<PlayerState>();

            // 目前行動中的玩家編號
            // 0 = 1P（畫面下方） 1 = 2P（畫面上方）
            short _cPlayer;
            public short CurrentPlayer
            {
                get
                {
                    return _cPlayer;
                }
                set
                {
                    _cPlayer = value;
                }
            }
            public void SwitchPlayer()
            {
                if (_cPlayer == 0) _cPlayer = 1; else _cPlayer = 0;
                _chosenPieceNo = 0;
            }

            short _chosenPieceNo;
            /// <summary>
            /// 目前選取的Piece是Reserves中的第幾個
            /// </summary>
            public short ChosenPiece
            {
                get
                {
                    return _chosenPieceNo;
                }
                set
                {
                    _chosenPieceNo = value;
                    UpdateCurrentPieceShape();
                }
            }
            public PieceShape CurrentPieceShape;
            /// <summary>
            /// 旋轉目前選定的Piece，預設為順時針
            /// </summary>
            /// <param name="clockwise">是否為順時針旋轉</param>
            public void Rotate(bool clockwise = true)
            {
                if (clockwise)
                {
                    Player[_cPlayer].Reserves[_chosenPieceNo] = GetRotatedPieceShape(Player[_cPlayer].Reserves[_chosenPieceNo], 1);
                }
                else
                {
                    Player[_cPlayer].Reserves[_chosenPieceNo] = GetRotatedPieceShape(Player[_cPlayer].Reserves[_chosenPieceNo], -1);
                }
                UpdateCurrentPieceShape();
            }
            public void UpdateCurrentPieceShape()
            {
                CurrentPieceShape = Player[_cPlayer].Reserves[_chosenPieceNo];
            }

            /// <summary>
            /// 根據輸入的Piece種類與旋轉角度，傳回最終的Piece形狀
            /// </summary>
            /// <param name="piece">原始Piece</param>
            /// <param name="rotation">旋轉方式，支援 1-2-3（順時針90-180-270度）和 1 / -1（順時針90 / 逆時針90度）兩種格式</param>
            /// <returns></returns>
            public PieceShape GetRotatedPieceShape(PieceShape piece, short rotation)
            {
                PieceShape newPiece;
                switch (rotation)
                {
                    case 1:
                        // 順時針90度：876543210 => 258147036
                        newPiece = new PieceShape(piece.Tile6, piece.Tile3, piece.Tile0, piece.Tile7, piece.Tile4, piece.Tile1, piece.Tile8, piece.Tile5, piece.Tile2);
                        return newPiece;
                    case 2:
                        // 順時針180度：876543210 => 012345678
                        newPiece = new PieceShape(piece.Tile8, piece.Tile7, piece.Tile6, piece.Tile5, piece.Tile4, piece.Tile3, piece.Tile2, piece.Tile1, piece.Tile0);
                        return newPiece;
                    case -1:
                    case 3:
                        // 逆時針90度：876543210 => 630741852
                        newPiece = new PieceShape(piece.Tile2, piece.Tile5, piece.Tile8, piece.Tile1, piece.Tile4, piece.Tile7, piece.Tile0, piece.Tile3, piece.Tile6);
                        return newPiece;
                    default:
                        return piece;
                }
            }
        }

        /// <summary>
        /// 動畫狀態
        /// </summary>
        class MovementState
        {
            public MovementState(short tile_x, short tile_y, double time)
            {
                TileX = tile_x;
                TileY = tile_y;
                TotalDuration = time;
                ElapsedDuration = 0;
            }

            public bool IsAnimating = false;
            public short TileX;
            public short TileY;
            public double TotalDuration;
            public double ElapsedDuration;

            /// <summary>
            /// 累積經過時間移動位置，如果時間到則傳回True並將IsAnimating狀態關掉
            /// </summary>
            /// <param name="ms">累積的經過時間</param>
            /// <returns></returns>
            public bool TimerUpdate(double ms)
            {
                bool t = false;
                ElapsedDuration += ms;
                if (ElapsedDuration >= TotalDuration)
                {
                    IsAnimating = false;
                    t = true;
                }
                return t;
            }
        }

        // 資源存放區
        Texture2D TEX_TILE, TEX_NEXT, TEX_RESERVES;
        Texture2D TEX_KEYS, TEX_CONTROLS, TEX_EXITRETRY;
        Texture2D TEX_BORDER;
        Texture2D TEX_AVATAR_IDLE, TEX_AVATAR_FALL, TEX_AVATAR_DEAD, TEX_AVATAR_VICTORY;
        Texture2D TEX_MOB_HOP, TEX_MOB_TURN, TEX_MOB_IDLE;
        Texture2D TEX_BOMB, TEX_PODIUM, TEX_BACKDROP, TEX_GAUGE_BORDER, TEX_GAUGE;

        // 音效
        SoundPlayer soundPlayer;


        /// <summary>
        ///  音效編號
        /// </summary>
        public enum SFX
        {
            Intro = 0,
            MainLoop = 1,
            MatchEnd = 2,
            CursorMove = 3,
            Rotate = 4,
            ReserveSelect = 5,
            Place = 6,
            Bomb = 7
        }

        // 盤面layout設定區
        const short TILE_WIDTH = 32, TILE_HEIGHT = 32, BORDER_THICKNESS = 16;
        const short KEY_WIDTH = 36, KEY_HEIGHT = 36;
        const short BOMB_WIDTH = 128, BOMB_HEIGHT = 128, BOMB_FRAMES = 6, BOMB_INTERVAL = 5;
        static short VIEW_WIDTH = 1024, VIEW_HEIGHT = 720;
        short BOARD_MAX_Y = 10, BOARD_MAX_X = 10;
        short BOMB_COST = 20, BOMBGAUGE_MAX = 40;

        short RESERVES_WIDTH, RESERVES_HEIGHT, NEXT_WIDTH, NEXT_HEIGHT;
        Vector2 BOARD_TOPLEFT, RESERVE_TOPLEFT_1P, NEXT_PIECE_TOPLEFT_1P, RESERVE_TOPLEFT_2P, NEXT_PIECE_TOPLEFT_2P;
        const short CONTROLS_WIDTH = 184, CONTROLS_HEIGHT = 150, EXITRETRY_WIDTH = 152, EXITRETRY_HEIGHT = 54;
        Vector2 CONTROLS_XY;

        // 動畫layout設定區
        const short AVATAR_WIDTH = 160, AVATAR_HEIGHT = 200;
        const short MOB_WIDTH = 80, MOB_HEIGHT = 100;
        const short AVATAR_FRAMERATE = 12, EFFECT_FRAMERATE = 24, AVATAR_IDLE_FRAMES = 24, AVATAR_FALL_FRAMES = 10, AVATAR_DEAD_FRAMES = 12, AVATAR_VICTORY_FRAMES = 18;
        const short MOB_HOP_FRAMES = 8, MOB_TURN_FRAMES = 4, MOB_IDLE_FRAMES = 8;
        const float MOB_MIN_Y = 230;
        const int MOB_MAX = 200, MOB_SPAWN_INTERVAL = 10;  // 圍觀球兔上限人數、出現頻率
        static Vector2 AVATAR_TOPLEFT_1P, AVATAR_TOPLEFT_2P, PODIUM_TOPLEFT_1P, PODIUM_TOPLEFT_2P, BOMBGAUGE_TOPLEFT_1P, BOMBGAUGE_TOPLEFT_2P;
        static Rectangle BOMBDOT_1P, BOMBDOT_2P;

        // Piece移動動畫總毫秒數
        static short PIECE_MOVE_TIME = 100;

        // 色彩
        Color COLOR_UNPLACEABLE = new Color(255, 255, 0, 255);
        Color COLOR_PLACEABLE = new Color(255, 255, 255, 128);
        Color COLOR_GAUGE_FILLED = new Color(0, 255, 0, 255);
        Color COLOR_GAUGE_EMPTY = new Color(0, 0, 0, 255);

        // 存放盤面的陣列，Content Load時再決定實際大小
        // 盤面中0代表空格，1代表1P佔領，2代表2P佔領
        // 所以將Piece填入盤面時要將Player編號+1
        short[,] Board;

        // 存放Piece類別的資料陣列
        // 每種類別都是3x3共9格的Tile，所以需要9位元來儲存
        List<PieceShape> PieceTypes;
        public struct PieceShape
        {
            public bool Tile0, Tile1, Tile2, Tile3, Tile4, Tile5, Tile6, Tile7, Tile8;
            /// <summary>
            /// 以字串建構Piece形狀
            /// </summary>
            /// <param name="tiles">9個字元的字串，1代表有方塊，其他字元（建議用0）代表沒有方塊</param>
            public PieceShape(string tiles)
            {
                Tile0 = tiles[0] == '1';
                Tile1 = tiles[1] == '1';
                Tile2 = tiles[2] == '1';
                Tile3 = tiles[3] == '1';
                Tile4 = tiles[4] == '1';
                Tile5 = tiles[5] == '1';
                Tile6 = tiles[6] == '1';
                Tile7 = tiles[7] == '1';
                Tile8 = tiles[8] == '1';
            }
            /// <summary>
            /// 以九個bool值建構Piece形狀
            /// </summary>
            public PieceShape(bool t0, bool t1, bool t2, bool t3, bool t4, bool t5, bool t6, bool t7, bool t8)
            {
                Tile0 = t0;
                Tile1 = t1;
                Tile2 = t2;
                Tile3 = t3;
                Tile4 = t4;
                Tile5 = t5;
                Tile6 = t6;
                Tile7 = t7;
                Tile8 = t8;
            }
            public bool GetTile(int tileNo)
            {
                switch (tileNo)
                {
                    case 0: return Tile0;
                    case 1: return Tile1;
                    case 2: return Tile2;
                    case 3: return Tile3;
                    case 4: return Tile4;
                    case 5: return Tile5;
                    case 6: return Tile6;
                    case 7: return Tile7;
                    default: return Tile8;
                }
            }
        }

        // 圖層專用Enum（由前至後）
        public enum Layer
        {
            Message,
            FrontEffect,
            PlacingPiece,
            PiecePreview,
            PlacedPieces,
            PoolSelection,
            PoolBorder,
            AvatarAnimation,
            BoardBackground,
            GaugeBorder,
            Gauge,
            Podium,
            Mobs,
            Background,
            MAX
        }

        /// <summary>
        /// 特效動畫
        /// </summary>
        public class EffectAnimation
        {
            public short X, Y;
            public bool Looping = false;
            public Texture2D AnimationTexture;
            public Vector2 AnimationPosition;
            public short FrameRate;
            short _cFrame = 0;
            public short CurrentFrame
            {
                get
                {
                    return (short)(_cFrame / (60 / FrameRate) + 1);
                }
                set
                {
                    _cFrame = (short)(value * (int)(60 / FrameRate) - 1);
                }
            }

            public short FramesToDelay, FramesPerLoop;
            public AnimationState State;
            public enum AnimationState
            {
                Delaying, Playing, Ended
            }

            public EffectAnimation(Texture2D texture, Vector2 position, short totalFrames, short delayFrames = 0, short tileX = 0, short tileY = 0, short fps = EFFECT_FRAMERATE, bool loops = false)
            {
                X = tileX;
                Y = tileY;
                AnimationTexture = texture;
                AnimationPosition = position;
                FramesToDelay = delayFrames;
                FramesPerLoop = totalFrames;
                FrameRate = fps;
                Looping = loops;
                if (delayFrames > 0) State = AnimationState.Delaying; else State = AnimationState.Playing;
            }

            public void FrameForward()
            {
                switch (State)
                {
                    case AnimationState.Delaying:
                        FramesToDelay--;
                        if (FramesToDelay == 0) State = AnimationState.Playing;
                        break;
                    case AnimationState.Playing:
                        _cFrame++;
                        if (CurrentFrame > FramesPerLoop)
                        {
                            if (Looping) _cFrame = 0; else State = AnimationState.Ended;
                        }
                        break;
                }
            }
        }

        // 動畫排程
        List<EffectAnimation> BombAnimations;   // 炸彈動畫
        EffectAnimation ControlsAnimation;      // 操作說明動畫

        /// <summary>
        /// 圍觀球兔
        /// </summary>
        public class Mob
        {
            public enum MobStates { Hopping, Turning, Idle }
            public MobStates MobState = MobStates.Hopping;
            public float CurrentX, TargetX, TargetY, Scale;
            public bool LeftEntry = true;
            short _cFrame;
            public short CurrentFrame
            {
                get
                {
                    return (short)(_cFrame / (60 / AVATAR_FRAMERATE) + 1);
                }
                set
                {
                    _cFrame = (short)(value * (int)(60 / AVATAR_FRAMERATE) - 1);
                }
            }

            public Mob(float x, float y)
            {
                TargetX = x;
                TargetY = y;
                Scale = (y - MOB_MIN_Y) / (PODIUM_TOPLEFT_1P.Y - MOB_HEIGHT - MOB_MIN_Y) * .5f + .5f;

                // 決定圍觀球兔的進場延遲
                if (x > VIEW_WIDTH / 2)
                {
                    LeftEntry = false;
                    CurrentX = VIEW_WIDTH + (TargetX % AVATAR_FRAMERATE);
                }
                else
                {
                    CurrentX = -MOB_WIDTH - (TargetX % AVATAR_FRAMERATE);
                }
            }

            /// <summary>
            /// 推進圍觀球兔動畫
            /// </summary>
            public void MobFrameForward()
            {
                _cFrame++;
                switch (MobState)
                {
                    case MobStates.Hopping:
                        // Hopping：還在蹦跳移動中
                        if (LeftEntry) CurrentX++; else CurrentX--;
                        // 落地後如果已超過目標位置就接Turn
                        if (CurrentFrame > MOB_HOP_FRAMES)
                        {
                            if ((LeftEntry && CurrentX >= TargetX) || (!LeftEntry && CurrentX <= TargetX))
                            {
                                MobState = MobStates.Turning;
                            }
                            _cFrame = 0;
                        }
                        break;
                    case MobStates.Turning:
                        // Turn：播完接Idle
                        if (CurrentFrame > MOB_TURN_FRAMES)
                        {
                            _cFrame = 0;
                            MobState = MobStates.Idle;
                        }
                        break;
                    case MobStates.Idle:
                        // Idle：迴圈
                        if (CurrentFrame > MOB_IDLE_FRAMES) _cFrame = 0;
                        break;
                }
            }
        }
        List<Mob> MobAnimations;

        // 輸入
        MouseState mouPrevious, mouCurrent;
        KeyboardState keyPrevious, keyCurrent;

        // 鍵盤定義
        static Keys KEY_EXIT = Keys.Escape;
        static Keys KEY_REMATCH = Keys.Space;
        static Keys KEY_CHOICE_1 = Keys.A, KEY_CHOICE_2 = Keys.S, KEY_CHOICE_3 = Keys.D;
        static Keys KEY_BOMB = Keys.B;
        static Keys KEY_ROTATE_COUNTERCLOCKWISE = Keys.Q, KEY_ROTATE_CLOCKWISE = Keys.W;

        // 音量
        static float VOLUME_SFX = .5f, VOLUME_BGM = 1f;

        public GameBoard()
        {
            graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
        }

        /// <summary>
        /// Allows the game to perform any initialization it needs to before starting to run.
        /// This is where it can query for any required services and load any non-graphic
        /// related content.  Calling base.Initialize will enumerate through any components
        /// and initialize them as well.
        /// </summary>
        protected override void Initialize()
        {
            graphics.PreferMultiSampling = false;
            IsMouseVisible = true;

            // 設定畫面
            graphics.IsFullScreen = false;
            graphics.PreferredBackBufferWidth = VIEW_WIDTH;
            graphics.PreferredBackBufferHeight = VIEW_HEIGHT;
            graphics.ApplyChanges();

            random = new Random();

            base.Initialize();
        }

        protected override void LoadContent()
        {
            spriteBatch = new SpriteBatch(GraphicsDevice);

            TEX_TILE = Content.Load<Texture2D>("Sprites\\tiles");
            TEX_BORDER = Content.Load<Texture2D>("Sprites\\border");
            TEX_NEXT = Content.Load<Texture2D>("Sprites\\next");
            TEX_KEYS = Content.Load<Texture2D>("Sprites\\keys");
            TEX_CONTROLS = Content.Load<Texture2D>("Sprites\\controls");
            TEX_EXITRETRY = Content.Load<Texture2D>("Sprites\\inout");
            TEX_AVATAR_IDLE = Content.Load<Texture2D>("Sprites\\br-idle");
            TEX_AVATAR_FALL = Content.Load<Texture2D>("Sprites\\br-fall");
            TEX_AVATAR_DEAD = Content.Load<Texture2D>("Sprites\\br-dead");
            TEX_AVATAR_VICTORY = Content.Load<Texture2D>("Sprites\\br-victory");
            TEX_MOB_HOP = Content.Load<Texture2D>("Sprites\\mob-hop");
            TEX_MOB_TURN = Content.Load<Texture2D>("Sprites\\mob-turn");
            TEX_MOB_IDLE = Content.Load<Texture2D>("Sprites\\mob-idle");
            TEX_BOMB = Content.Load<Texture2D>("Sprites\\bomb");
            TEX_PODIUM = Content.Load<Texture2D>("Sprites\\podium");
            TEX_GAUGE_BORDER = Content.Load<Texture2D>("Sprites\\gauge");
            TEX_GAUGE = Content.Load<Texture2D>("Sprites\\gaugedot");

            // 音效
            soundPlayer = new SoundPlayer();
            soundPlayer.Initialize(Content.RootDirectory + "\\" + "Audio");
            soundPlayer.Clear();
            soundPlayer.AddSound("intro.ogg", VOLUME_BGM, false, true);
            soundPlayer.AddSound("loop.ogg", VOLUME_BGM, true);
            soundPlayer.AddSound("matchend.ogg", VOLUME_BGM);
            soundPlayer.AddSound("cursor_move.ogg", VOLUME_SFX);
            soundPlayer.AddSound("rotate.ogg", VOLUME_SFX);
            soundPlayer.AddSound("select.ogg", VOLUME_SFX);
            soundPlayer.AddSound("place.ogg", VOLUME_SFX);
            soundPlayer.AddSound("bomb.ogg", VOLUME_SFX);

            // 設定各項layout位置

            // 設定Avatar位置
            // Avatar位置是Pool列以外高度置中，與盤面邊緣距離固定
            AVATAR_TOPLEFT_1P = new Vector2(VIEW_WIDTH - AVATAR_WIDTH - 64, (VIEW_HEIGHT - RESERVES_HEIGHT - AVATAR_HEIGHT + RESERVES_HEIGHT) / 2);
            AVATAR_TOPLEFT_2P = new Vector2(64, AVATAR_TOPLEFT_1P.Y);
            PODIUM_TOPLEFT_1P = new Vector2(VIEW_WIDTH - TEX_PODIUM.Width - 64 + (TEX_PODIUM.Width - AVATAR_WIDTH) / 2, AVATAR_TOPLEFT_1P.Y + (AVATAR_HEIGHT - TEX_PODIUM.Height) * 1.4f);
            PODIUM_TOPLEFT_2P = new Vector2(64 - (TEX_PODIUM.Width - AVATAR_WIDTH) / 2, PODIUM_TOPLEFT_1P.Y);
            BOMBGAUGE_TOPLEFT_1P = new Vector2(VIEW_WIDTH - TEX_GAUGE_BORDER.Width - 64 + (TEX_GAUGE_BORDER.Width - AVATAR_WIDTH) / 2, PODIUM_TOPLEFT_1P.Y + TEX_PODIUM.Height - TEX_GAUGE_BORDER.Height * 1.4f);
            BOMBGAUGE_TOPLEFT_2P = new Vector2(64 - (TEX_GAUGE_BORDER.Width - AVATAR_WIDTH) / 2, BOMBGAUGE_TOPLEFT_1P.Y);
            BOMBDOT_1P = new Rectangle((int)(BOMBGAUGE_TOPLEFT_1P.X + 7), (int)(BOMBGAUGE_TOPLEFT_1P.Y + 4), TEX_GAUGE_BORDER.Width - 14, TEX_GAUGE_BORDER.Height - 8);
            BOMBDOT_2P = new Rectangle((int)(BOMBGAUGE_TOPLEFT_2P.X + 7), (int)(BOMBGAUGE_TOPLEFT_2P.Y + 4), TEX_GAUGE_BORDER.Width - 14, TEX_GAUGE_BORDER.Height - 8);

            // 設定Pool起點位置
            // Next Piece最大寬度是3格
            // Reserves的寬度是(Piece最大寬度3格*3)=9格 + Piece之間各1格緩衝共2格 + 左右各1格緩衝共2格 + 邊框共2格緩衝，與盤面一樣置中
            // 因此第一個Piece的左端是Reserves左端 + 左邊框1格 + 左端0.5格緩衝，共1.5格
            // Reserves的高度是Piece最大高度3格 + 上下各1格緩衝共2格 + 邊框共2格緩衝
            // 因此第一個Piece的頂端是Reserves頂端 + 上邊框1格 + 上端1格，共2格
            RESERVES_WIDTH = TILE_WIDTH * 15;
            RESERVES_HEIGHT = TILE_HEIGHT * 7;
            // 1P側Reserves位於盤面下方
            RESERVE_TOPLEFT_1P = new Vector2((VIEW_WIDTH - RESERVES_WIDTH) / 2, VIEW_HEIGHT - RESERVES_HEIGHT + 48);
            // 2P側Reserves位於盤面上方
            RESERVE_TOPLEFT_2P = new Vector2((VIEW_WIDTH - RESERVES_WIDTH) / 2, -48);

            // Next Piece Pool的寬度是Piece最大寬度3格 + 左右各0.5格緩衝共1格 + 邊框共2格緩衝，從畫面右端逆算位置
            // Next Piece Pool的高度和Reserves一樣
            NEXT_WIDTH = TILE_WIDTH * 6;
            NEXT_HEIGHT = RESERVES_HEIGHT;
            // 1P側Next Piece位於左下方，與邊緣固定距離
            NEXT_PIECE_TOPLEFT_1P = new Vector2(64, VIEW_HEIGHT - NEXT_HEIGHT + 48);
            // 2P側Next Piece位於右上方，與邊緣固定距離
            NEXT_PIECE_TOPLEFT_2P = new Vector2(VIEW_WIDTH - NEXT_WIDTH - 64, -48);
            // 操作說明位於右下方，與邊緣固定距離
            CONTROLS_XY = new Vector2(VIEW_WIDTH - CONTROLS_WIDTH - KEY_WIDTH, VIEW_HEIGHT - CONTROLS_HEIGHT - KEY_HEIGHT * 0.5f);

            // 設定盤面起點位置
            // 盤面置中
            BOARD_TOPLEFT = new Vector2((VIEW_WIDTH - TILE_WIDTH * BOARD_MAX_X) / 2, (VIEW_HEIGHT - TILE_HEIGHT * BOARD_MAX_Y) / 2);

            // 生成Piece種類
            // == Piece 種類 ==
            // 0:方塊  1:S形   2:L形   3:I形   4:小L形 5:T形   6:大S形 7:大L形 8:短I形 9: 逆S  10: 逆大S 11: W形
            // ■■□  □□■  ■■□  □■□  □■□  □■□  □□■  ■■■  □■□  ■□□  ■□□    □□■
            // ■■□  □■■  □■□  □■□  ■■□  ■■■  ■■■  □□■  □■□  ■■□  ■■■    □■■ 
            // □□□  □■□  □■□  □■□  □□□  □□□  ■□□  □□■  □□□  □■□  □□■    ■■□
            PieceTypes = new List<PieceShape>();
            PieceTypes.Add(new PieceShape("110110000"));
            PieceTypes.Add(new PieceShape("001011010"));
            PieceTypes.Add(new PieceShape("110010010"));
            PieceTypes.Add(new PieceShape("010010010"));
            PieceTypes.Add(new PieceShape("010110000"));
            PieceTypes.Add(new PieceShape("010111000"));
            PieceTypes.Add(new PieceShape("001111100"));
            PieceTypes.Add(new PieceShape("111001001"));
            PieceTypes.Add(new PieceShape("010010000"));
            PieceTypes.Add(new PieceShape("100110010"));
            PieceTypes.Add(new PieceShape("100111001"));
            PieceTypes.Add(new PieceShape("001011110"));

            // 準備玩家
            CurrentMatchState = new MatchState();
            CurrentMatchState.Player.Add(new MatchState.PlayerState());
            CurrentMatchState.Player.Add(new MatchState.PlayerState());

            // 讀取滑鼠鍵盤狀態
            mouCurrent = Mouse.GetState();
            mouPrevious = mouCurrent;
            keyCurrent = Keyboard.GetState();
            keyPrevious = keyCurrent;

            // 準備動畫
            BombAnimations = new List<EffectAnimation>();
            MobAnimations = new List<Mob>();
            ControlsAnimation = new EffectAnimation(TEX_CONTROLS, CONTROLS_XY, 20, 0, 0, 0, 12, true);

            TEX_RESERVES = Content.Load<Texture2D>("Sprites\\reserves");
            TEX_BACKDROP = Content.Load<Texture2D>("Sprites\\backdrop");

            // 生成盤面、雙方的Reserves和Next Piece等
            InitializeMatch();

        }

        /// <summary>
        /// UnloadContent will be called once per game and is the place to unload
        /// game-specific content.
        /// </summary>
        protected override void UnloadContent()
        {
            soundPlayer.Dispose();
            Dispose();
        }

        protected override void Update(GameTime gameTime)
        {
            if (Keyboard.GetState().IsKeyDown(Keys.Escape))
                Exit();

            // Intro銜接Loop的工作交給SoundPlayer做
            soundPlayer.Update();
            // BGM在一方敗北時銜接到結束曲
            if (CurrentMatchState.Phase == MatchPhase.Ended && soundPlayer.IsPlaying((int)SFX.MainLoop))
            {
                uint approach = soundPlayer.GetPosition((int)SFX.MainLoop) % 441;
                if (approach < 50 || approach > 391)
                {
                    // 如果時機夠接近了就換歌
                    soundPlayer.StopSound((int)SFX.MainLoop);
                    soundPlayer.StartSound((int)SFX.MatchEnd);
                }
                else
                {
                    // 不夠的話就fade out
                    soundPlayer.SetVolume((int)SFX.MainLoop, soundPlayer.GetVolume((int)SFX.MainLoop) * .9f);
                }
            }

            // 更新輸入
            UpdateMouseInput();
            UpdateKeyInput();

            // 更新操作說明動畫
            ControlsAnimation.FrameForward();
            CurrentMatchState.Player[0].AvatarFrameForward();
            CurrentMatchState.Player[1].AvatarFrameForward();

            // 更新集氣條顯示值
            if (CurrentMatchState.Player[0].DisplayedBombGauge < CurrentMatchState.Player[0].BombGauge) CurrentMatchState.Player[0].DisplayedBombGauge++;
            if (CurrentMatchState.Player[0].DisplayedBombGauge > CurrentMatchState.Player[0].BombGauge) CurrentMatchState.Player[0].DisplayedBombGauge -= 2;
            if (CurrentMatchState.Player[1].DisplayedBombGauge < CurrentMatchState.Player[1].BombGauge) CurrentMatchState.Player[1].DisplayedBombGauge++;
            if (CurrentMatchState.Player[1].DisplayedBombGauge > CurrentMatchState.Player[1].BombGauge) CurrentMatchState.Player[1].DisplayedBombGauge -= 2;

            // 更新炸彈動畫
            foreach (EffectAnimation obj in BombAnimations)
            {
                obj.FrameForward();
                if (obj.State == EffectAnimation.AnimationState.Ended) BombClear(obj.X, obj.Y);
            }
            BombAnimations.RemoveAll(obj => obj.State == EffectAnimation.AnimationState.Ended);

            // 更新圍觀球兔動畫
            MobAnimations.ForEach(obj => obj.MobFrameForward());

            switch (CurrentMatchState.Phase)
            {
                case MatchPhase.TurnStart:
                    Update_TurnStart();
                    break;
                case MatchPhase.AITurnStart:
                    // AI回合開始時稍作停頓
                    CurrentMatchState.AIActionTimer += gameTime.ElapsedGameTime.TotalMilliseconds;
                    if (CurrentMatchState.AIActionTimer >= .5)
                    {
                        CurrentMatchState.Phase = MatchPhase.AIPlacement;
                        CurrentMatchState.AIActionTimer = 0;
                    }
                    break;
                case MatchPhase.AIPlacement:
                    Update_AIPlacement(gameTime);
                    break;
                case MatchPhase.AfterPlacement:
                    Update_Afterplacement();
                    break;
            }
            base.Update(gameTime);
        }

        private void Update_Afterplacement()
        {
            // 判斷放置Piece動畫是否播完
            if (!CurrentMatchState.PlacementAnimation.IsAnimating)
            {
                // 動畫已播完
                // 實際將Piece存入盤面
                PlacePiece(CurrentMatchState.PlacementAnimation.TileX, CurrentMatchState.PlacementAnimation.TileY, CurrentMatchState.CurrentPieceShape);

                // 遞補Piece
                CurrentMatchState.Player[CurrentMatchState.CurrentPlayer].Reserves[CurrentMatchState.ChosenPiece] = CurrentMatchState.Player[CurrentMatchState.CurrentPlayer].NextPiece;
                CurrentMatchState.Player[CurrentMatchState.CurrentPlayer].NextPiece = PieceTypes[random.Next(0, PieceTypes.Count)];

                // 替換玩家
                CurrentMatchState.SwitchPlayer();
                CurrentMatchState.UpdateCurrentPieceShape();
                CurrentMatchState.AIActionTimer = 0;

                // 回合數增加
                CurrentMatchState.Turns++;
                // 在場上增加圍觀球兔（有上限）
                // TODO：想辦法讓球兔不會撞位置？
                if (CurrentMatchState.Turns % MOB_SPAWN_INTERVAL == 0 && MobAnimations.Count < MOB_MAX)
                {
                    float dist = (float)random.NextDouble() * BOARD_TOPLEFT.X - MOB_WIDTH;
                    // 有一半機率從右邊入場
                    if (random.NextDouble() > .5) dist = VIEW_WIDTH - dist - MOB_WIDTH;
                    MobAnimations.Add(new Mob(dist, (float)random.NextDouble() * (PODIUM_TOPLEFT_1P.Y - MOB_HEIGHT - MOB_MIN_Y) + MOB_MIN_Y));
                }

                CurrentMatchState.Phase = MatchPhase.TurnStart;
            }
        }

        private void Update_AIPlacement(GameTime gameTime)
        {
            if (CurrentMatchState.AIActionTimer == 0)
            {
                // 進入AIPlacement階段的瞬間，AI開始思考
                short xPick, yPick;
                short reservePick, rotationPick;
                GetPlaceable(out xPick, out yPick, out reservePick, out rotationPick);
                // 思考完畢，確定放置
                soundPlayer.StartSound((int)SFX.Place);

                // 先旋轉reserve
                CurrentMatchState.Player[CurrentMatchState.CurrentPlayer].Reserves[reservePick] = CurrentMatchState.GetRotatedPieceShape(CurrentMatchState.Player[CurrentMatchState.CurrentPlayer].Reserves[reservePick], rotationPick);
                // 再選擇reserve，才會在ChosenPiece過程中正確更新形狀
                CurrentMatchState.ChosenPiece = reservePick;
                CurrentMatchState.PointedTileX = xPick;
                CurrentMatchState.PointedTileY = yPick;
                CurrentMatchState.Phase = MatchPhase.AfterPlacement;
                CurrentMatchState.PlacementAnimation = new MovementState(CurrentMatchState.PointedTileX, CurrentMatchState.PointedTileY, PIECE_MOVE_TIME);
                CurrentMatchState.PlacementAnimation.IsAnimating = true;
            }
            CurrentMatchState.AIActionTimer += gameTime.ElapsedGameTime.TotalMilliseconds;
        }

        private void Update_TurnStart()
        {
            // 如果有炸彈動畫正在播放就先停頓
            // （炸彈播放時Phase一定會是TurnStart）
            if (BombAnimations.Count > 0) return;
            // 判定是否有步可下，有則移至Placement階段，否則移至Ended階段
            // 針對三個Reserves判定盤面上是否有空間可放，只要有就停止判定

            PieceShape shapeToFind;
            bool isAlive = false;

            for (int i = 0; i < 3; i++)
            {
                shapeToFind = CurrentMatchState.Player[CurrentMatchState.CurrentPlayer].Reserves[i];
                // 偵測各種旋轉後的形狀
                for (int r = 0; r < 4; r++)
                {
                    for (int x = 0; x <= BOARD_MAX_X; x++)
                    {
                        for (int y = 0; y <= BOARD_MAX_Y; y++)
                        {
                            if (IsPlaceable(shapeToFind, x, y))
                            {
                                isAlive = true;

                                // 可以進行回合
                                if (CurrentMatchState.VSMode || CurrentMatchState.CurrentPlayer == 0)
                                {
                                    // 如果是玩家的回合就進入Placement階段
                                    CurrentMatchState.Phase = MatchPhase.Placement;
                                }
                                else
                                {
                                    // 如果是電腦的回合就進入AIPlacement階段並直接呼叫AI思考程序
                                    // AI思考程序內會自動決定行動並推進到AfterPlacement階段
                                    CurrentMatchState.Phase = MatchPhase.AITurnStart;
                                }
                                break;
                            }
                        }
                    }
                    // 轉一次
                    shapeToFind = CurrentMatchState.GetRotatedPieceShape(shapeToFind, 1);
                }
            }

            // 如果有炸彈就無條件可以行動
            if (CurrentMatchState.Player[CurrentMatchState.CurrentPlayer].BombGauge >= BOMB_COST)
            {
                if (!CurrentMatchState.VSMode && CurrentMatchState.CurrentPlayer == 1)
                {
                    // 如果輪到AI行動且不用炸彈不行就會自動用炸彈
                    if (!isAlive)
                    {
                        CurrentMatchState.Player[CurrentMatchState.CurrentPlayer].BombGauge -= BOMB_COST;
                        BombBoard();
                        // 不改變階段，仍在TurnStart
                    }
                }
                else
                {
                    // 如果是玩家的回合就進入Placement階段，由玩家操作
                    CurrentMatchState.Phase = MatchPhase.Placement;
                }
                // 不管哪種情形都不播放敗北動畫
                isAlive = true;
            }

            if (!isAlive)
            {
                // 播放敗北玩家的Fall動畫、勝利玩家的Victory動畫
                short oppo;
                if (CurrentMatchState.CurrentPlayer == 0) oppo = 1; else oppo = 0;
                CurrentMatchState.Player[CurrentMatchState.CurrentPlayer].AvatarState = MatchState.PlayerState.AnimationState.Fall;
                CurrentMatchState.Player[oppo].AvatarState = MatchState.PlayerState.AnimationState.Victory;
                CurrentMatchState.Phase = MatchPhase.Ended;
            }
        }

        /// <summary>
        /// This is called when the game should draw itself.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Draw(GameTime gameTime)
        {
            switch (CurrentGameState)
            {
                case GameState.Match:
                    GraphicsDevice.Clear(Color.Black);
                    RestartSpriteBatch();
                    DrawMatch(gameTime);
                    break;
            }

            spriteBatch.End();
            base.Draw(gameTime);
        }

        void InitializeMatch()
        {
            // 生成盤面
            Board = new short[BOARD_MAX_X + 1, BOARD_MAX_Y + 1];

            for (int i = 0; i < CurrentMatchState.Player.Count; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    CurrentMatchState.Player[i].Reserves[j] = PieceTypes[random.Next(0, PieceTypes.Count)];
                }
                CurrentMatchState.Player[i].NextPiece = PieceTypes[random.Next(0, PieceTypes.Count)];
                CurrentMatchState.Player[i].AvatarState = MatchState.PlayerState.AnimationState.Idle;
                CurrentMatchState.Player[i].BombGauge = 0;
                CurrentMatchState.Player[i].DisplayedBombGauge = 0;
            }

            CurrentMatchState.CurrentPlayer = 0;
            CurrentMatchState.ChosenPiece = 0;

            // 清場
            MobAnimations.Clear();

            CurrentMatchState.Turns = 1;
            CurrentMatchState.Phase = MatchPhase.TurnStart;

            // 音效重置
            soundPlayer.AllStop();
            soundPlayer.SetVolume((int)SFX.Intro, VOLUME_BGM);
            soundPlayer.SetVolume((int)SFX.MainLoop, VOLUME_BGM);
            soundPlayer.SetVolume((int)SFX.MatchEnd, VOLUME_BGM);
            soundPlayer.SetVolume((int)SFX.CursorMove, VOLUME_SFX);
            soundPlayer.SetVolume((int)SFX.Bomb, VOLUME_SFX);
            soundPlayer.SetVolume((int)SFX.Place, VOLUME_SFX);
            soundPlayer.SetVolume((int)SFX.ReserveSelect, VOLUME_SFX);
            soundPlayer.SetVolume((int)SFX.Rotate, VOLUME_SFX);

            // 背景音樂從頭開始播
            soundPlayer.StartSound((int)SFX.Intro);
        }

        void UpdateMouseInput()
        {
            if (IsActive)
            {
                mouPrevious = mouCurrent;
                mouCurrent = Mouse.GetState();
                switch (CurrentGameState)
                {
                    case GameState.Match:
                        UpdateMouseInput_Match();
                        break;
                }
            }
        }

        private void UpdateMouseInput_Match()
        {
            // Match期間的操作
            Vector2 xy = new Vector2(mouCurrent.X, mouCurrent.Y);
            if (xy.X >= BOARD_TOPLEFT.X && xy.X < BOARD_TOPLEFT.X + TILE_WIDTH * BOARD_MAX_X && xy.Y >= BOARD_TOPLEFT.Y && xy.Y < BOARD_TOPLEFT.Y + TILE_HEIGHT * BOARD_MAX_Y)
            {
                Vector2 tilePointed = Vector2BoardTile(xy);
                // 如果是玩家的回合就更新目前所指的格子
                if (CurrentMatchState.VSMode || CurrentMatchState.CurrentPlayer == 0)
                {
                    CurrentMatchState.IsPointingTile = true;
                    // 如果游標指的格子有變就播放音效
                    if (CurrentMatchState.Phase == MatchPhase.Placement && (CurrentMatchState.PointedTileX != (short)tilePointed.X || CurrentMatchState.PointedTileY != (short)tilePointed.Y))
                    {
                        soundPlayer.StartSound((int)SFX.CursorMove);
                    }

                    // 儲存目前所指的格子
                    CurrentMatchState.PointedTileX = (short)tilePointed.X;
                    CurrentMatchState.PointedTileY = (short)tilePointed.Y;
                }

                // 判斷是否按下左鍵（放置Piece）
                // 只要在盤面內放開左鍵，按下時游標在盤面外也沒關係
                if (IsClicked())
                {
                    if (IsCurrentPiecePlaceable() && CurrentMatchState.Phase == MatchPhase.Placement)
                    {
                        // 確定放置，暫時鎖定操作
                        CurrentMatchState.Phase = MatchPhase.AfterPlacement;
                        CurrentMatchState.PlacementAnimation = new MovementState(CurrentMatchState.PointedTileX, CurrentMatchState.PointedTileY, PIECE_MOVE_TIME);
                        // 播放放置Piece的音效
                        soundPlayer.StartSound((int)SFX.Place);
                        CurrentMatchState.PlacementAnimation.IsAnimating = true;
                    }
                }

                // 判斷是否按下右鍵（旋轉Piece）
                // 預設為順時針旋轉
                if (IsClicked(false))
                {
                    // 播放旋轉音效
                    soundPlayer.StartSound((int)SFX.Rotate);
                    CurrentMatchState.Rotate();
                }
            }
            else
            {
                CurrentMatchState.IsPointingTile = false;

                // TODO：在盤面外的其他UI元素上放開左鍵時的動作（例如用滑鼠選擇Reserves等）
            }
        }

        void UpdateKeyInput()
        {
            if (IsActive)
            {
                // 更新按鍵變化
                keyPrevious = keyCurrent;
                keyCurrent = Keyboard.GetState();

                // Esc：退出遊戲
                if (IsKeyPressed(KEY_EXIT))
                {
                    UnloadContent();
                    Exit();
                }

                switch (CurrentGameState)
                {
                    case GameState.Match:
                        // Match期間的操作
                        switch (CurrentMatchState.Phase)
                        {
                            case MatchPhase.Placement:
                                // 自由操作期間才可操作

                                // 選擇Reserves的Piece
                                // 優先順序為從左至右
                                // 選擇時會有音效
                                if (IsKeyPressed(KEY_CHOICE_1, false))
                                {
                                    soundPlayer.StartSound((int)SFX.ReserveSelect);
                                    CurrentMatchState.ChosenPiece = 0;
                                }
                                else if (IsKeyPressed(KEY_CHOICE_2, false))
                                {
                                    soundPlayer.StartSound((int)SFX.ReserveSelect);
                                    CurrentMatchState.ChosenPiece = 1;
                                }
                                else if (IsKeyPressed(KEY_CHOICE_3, false))
                                {
                                    soundPlayer.StartSound((int)SFX.ReserveSelect);
                                    CurrentMatchState.ChosenPiece = 2;
                                }

                                // 旋轉選定的Piece
                                // 按下的瞬間就旋轉，直到放開前不會再旋轉，放開前按下另一邊的旋轉也無效
                                if (IsKeyPressed(KEY_ROTATE_COUNTERCLOCKWISE, false))
                                {
                                    soundPlayer.StartSound((int)SFX.Rotate);
                                    CurrentMatchState.Rotate(false);
                                }
                                else if (IsKeyPressed(KEY_ROTATE_CLOCKWISE, false))
                                {
                                    soundPlayer.StartSound((int)SFX.Rotate);
                                    CurrentMatchState.Rotate();
                                }

                                // 放炸彈
                                // 消耗氣力
                                // 隨機往盤面上兩個座標清除周圍九格
                                // 按鍵放開才算
                                if (IsKeyPressed(KEY_BOMB))
                                {
                                    // 氣不夠不能發動
                                    if (CurrentMatchState.Player[CurrentMatchState.CurrentPlayer].BombGauge >= BOMB_COST)
                                    {
                                        CurrentMatchState.Player[CurrentMatchState.CurrentPlayer].BombGauge -= BOMB_COST;
                                        BombBoard();
                                        CurrentMatchState.Phase = MatchPhase.TurnStart;
                                    }
                                }
                                break;
                            case MatchPhase.Ended:
                                // 重啟賽局
                                // 按鍵放開才算
                                if (IsKeyPressed(KEY_REMATCH))
                                {
                                    InitializeMatch();
                                }
                                break;
                        }
                        break;
                }
            }
        }

        /// <summary>
        /// 判斷按鍵是否被按下
        /// </summary>
        /// <param name="key">要偵測的按鍵</param>
        /// <param name="tapped">true：按下後放開　false：剛按下</param>
        /// <returns></returns>
        bool IsKeyPressed(Keys key, bool tapped = true)
        {
            return keyCurrent.IsKeyUp(key) & keyPrevious.IsKeyDown(key);
        }
        bool IsClicked(bool leftClick = true)
        {
            if (leftClick)
            {
                return mouCurrent.LeftButton == ButtonState.Released & mouPrevious.LeftButton == ButtonState.Pressed;
            }
            else
            {
                return mouCurrent.RightButton == ButtonState.Released & mouPrevious.RightButton == ButtonState.Pressed;
            }
        }

        Vector2 Vector2BoardTile(Vector2 xy)
        {
            float x = xy.X, y = xy.Y;
            x = (float)Math.Floor((x - BOARD_TOPLEFT.X) / TILE_WIDTH) + 1;
            y = (float)Math.Floor((y - BOARD_TOPLEFT.Y) / TILE_HEIGHT) + 1;
            return new Vector2(x, y);
        }
        Vector2 BoardTile2Vector(int tile_x, int tile_y)
        {
            return new Vector2((tile_x - 1) * TILE_WIDTH, (tile_y - 1) * TILE_HEIGHT) + BOARD_TOPLEFT;
        }

        /// <summary>
        /// 判斷CurrentMatchState所儲存的游標所指格子能不能放入Piece
        /// </summary>
        bool IsCurrentPiecePlaceable()
        {
            bool _is = true;
            int xReal = CurrentMatchState.PointedTileX - 1;
            int yReal = CurrentMatchState.PointedTileY - 1;

            // 開始判讀每一格
            for (int x = 0; x < 3; x++)
            {
                for (int y = 0; y < 3; y++)
                {
                    if (CurrentMatchState.CurrentPieceShape.GetTile(8 - y * 3 - x))
                    {
                        // 有Tile
                        // 1：不在盤面內
                        if (x + xReal <= 0 || x + xReal > BOARD_MAX_X || y + yReal <= 0 || y + yReal > BOARD_MAX_Y)
                        {
                            _is = false;
                        }
                        else
                        {
                            // 2：在盤面內但有其他Piece
                            if (Board[x + xReal, y + yReal] != 0)
                            {
                                _is = false;
                            }
                        }
                    }
                }
            }

            return _is;
        }

        /// <summary>
        /// 隨機挑選盤面上四個位置，登記在預定清除名單上並排程動畫
        /// </summary>
        void BombBoard()
        {
            int xBomb, yBomb;
            for (int i = 0; i < 4; i++)
            {
                xBomb = random.Next(1, BOARD_MAX_X + 1);
                yBomb = random.Next(1, BOARD_MAX_Y + 1);
                Vector2 posBomb = BoardTile2Vector(xBomb - 1, yBomb - 1) - new Vector2(TILE_WIDTH / 2, TILE_HEIGHT / 2);
                BombAnimations.Add(new EffectAnimation(TEX_BOMB, posBomb, BOMB_FRAMES, (short)(i * BOMB_INTERVAL), (short)xBomb, (short)yBomb));
            }
            // 音效
            soundPlayer.StartSound((int)SFX.Bomb);
        }

        void BombClear(int xBomb, int yBomb)
        {
            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    if (xBomb + x > 0 && xBomb + x <= BOARD_MAX_X && yBomb + y > 0 && yBomb + y <= BOARD_MAX_Y)
                    {
                        // 可以清除
                        Board[xBomb + x, yBomb + y] = 0;
                    }
                }
            }
        }

        /// <summary>
        /// 判斷指定的Piece在指定的座標能不能放置
        ///  指定的座標是Piece的左上角
        /// </summary>
        /// <param name="shape">指定的Piece形狀</param>
        /// <param name="tile_x">指定座標X</param>
        /// <param name="tile_y">指定座標Y</param>
        /// <returns></returns>
        bool IsPlaceable(PieceShape shape, int tile_x, int tile_y)
        {
            bool _is = true;

            // 開始判讀每一格
            for (int x = 0; x < 3; x++)
            {
                for (int y = 0; y < 3; y++)
                {
                    if (shape.GetTile(8 - y * 3 - x))
                    {
                        // 有Tile
                        // 1:不在盤面內
                        if (x + tile_x <= 0 || x + tile_x > BOARD_MAX_X || y + tile_y <= 0 || y + tile_y > BOARD_MAX_Y)
                        {
                            _is = false;
                        }
                        else
                        {
                            if (Board[x + tile_x, y + tile_y] != 0) _is = false;
                        }
                    }
                }
            }

            return _is;
        }

        /// <summary>
        /// 隨機判斷可以放置的Piece
        /// </summary>
        /// <param name="tile_x">可以放置Piece的X座標</param>
        /// <param name="tile_y">可以放置Piece的Y座標</param>
        /// <param name="reserveChoice">可以放置的Reserve編號</param>
        /// <param name="rotation">放置前必須旋轉的次數</param>
        void GetPlaceable(out short tile_x, out short tile_y, out short reserveChoice, out short rotation)
        {
            short xHigh = 0, yHigh = 0;
            short rotationHigh = 0, reserveHigh = 0;
            double scoreHigh = 0;

            // 掃一遍盤面，一遇到可放置的狀況就隨機骰一個分數，掃完之後分數最高的就是答案
            for (short rotationTest = 0; rotationTest <= 3; rotationTest++)
            {
                for (short xTest = 1; xTest <= BOARD_MAX_X; xTest++)
                {
                    for (short yTest = 1; yTest <= BOARD_MAX_Y; yTest++)
                    {
                        for (short reserveTest = 0; reserveTest <= 2; reserveTest++)
                        {
                            if (IsPlaceable(CurrentMatchState.GetRotatedPieceShape(CurrentMatchState.Player[CurrentMatchState.CurrentPlayer].Reserves[reserveTest], rotationTest), xTest - 1, yTest - 1))
                            {
                                double scoreTest = random.NextDouble() * 100;
                                if (scoreTest > scoreHigh)
                                {
                                    scoreHigh = scoreTest;
                                    xHigh = xTest;
                                    yHigh = yTest;
                                    rotationHigh = rotationTest;
                                    reserveHigh = reserveTest;
                                }
                            }
                        }
                    }
                }
            }

            tile_x = xHigh;
            tile_y = yHigh;
            reserveChoice = reserveHigh;
            rotation = rotationHigh;
        }

        /// <summary>
        /// 將指定的Piece shape放到指定的位置（指定位置是Piece的中心點）
        /// </summary>
        /// <param name="tile_x">放置的X座標</param>
        /// <param name="tile_y">放置的T座標</param>
        /// <param name="shape">放置的Piece形狀</param>
        void PlacePiece(short tile_x, short tile_y, PieceShape shape)
        {
            short xReal = (short)(tile_x - 1), yReal = (short)(tile_y - 1);

            // 開始判讀每一格
            for (short x = 0; x < 3; x++)
            {
                for (short y = 0; y < 3; y++)
                {
                    if (shape.GetTile(8 - y * 3 - x))
                    {
                        // 有Tile：填入盤面
                        Board[x + xReal, y + yReal] = (short)(CurrentMatchState.CurrentPlayer + 1);
                        // 幫放置Piece的玩家集氣
                        CurrentMatchState.Player[CurrentMatchState.CurrentPlayer].BombGauge++;
                        if (CurrentMatchState.Player[CurrentMatchState.CurrentPlayer].BombGauge > BOMBGAUGE_MAX)
                        {
                            CurrentMatchState.Player[CurrentMatchState.CurrentPlayer].BombGauge = BOMBGAUGE_MAX;
                        }
                    }
                }
            }
        }

        void RestartSpriteBatch()
        {
            spriteBatch.Begin(SpriteSortMode.BackToFront, alphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);
        }

        /// <summary>
        /// 繪製盤面
        /// </summary>
        void DrawMatch(GameTime gameTime)
        {
            // 繪製背景
            DrawObject(TEX_BACKDROP, Vector2.Zero, Layer.Background);

            // 顯示退出遊戲按鍵提示
            DrawFrame(TEX_KEYS, new Vector2(10 + KEY_WIDTH * .5f, 10 + (EXITRETRY_HEIGHT - KEY_HEIGHT) * .5f), 1, KEY_WIDTH, KEY_HEIGHT, (float)Layer.PlacedPieces);
            DrawFrame(TEX_EXITRETRY, new Vector2(20 + KEY_WIDTH * 2, 10), 1, EXITRETRY_WIDTH, EXITRETRY_HEIGHT, (float)Layer.PlacedPieces);
            // 如果勝負已分就顯示重玩按鍵提示，否則顯示一般操作提示
            if (CurrentMatchState.Phase == MatchPhase.Ended)
            {
                DrawFrame(TEX_KEYS, new Vector2(10, 20 + EXITRETRY_HEIGHT + (EXITRETRY_HEIGHT - KEY_HEIGHT) * .5f), 8, KEY_WIDTH, KEY_HEIGHT, (float)Layer.PlacedPieces);
                DrawFrame(TEX_KEYS, new Vector2(10 + KEY_WIDTH, 20 + EXITRETRY_HEIGHT + (EXITRETRY_HEIGHT - KEY_HEIGHT) * .5f), 9, KEY_WIDTH, KEY_HEIGHT, (float)Layer.PlacedPieces);
                DrawFrame(TEX_EXITRETRY, new Vector2(20 + KEY_WIDTH * 2, 20 + EXITRETRY_HEIGHT), 2, EXITRETRY_WIDTH, EXITRETRY_HEIGHT, (float)Layer.PlacedPieces);
            }
            else
            {
                // 繪製操作說明
                DrawFrame(ControlsAnimation.AnimationTexture, ControlsAnimation.AnimationPosition, ControlsAnimation.CurrentFrame, CONTROLS_WIDTH, CONTROLS_HEIGHT, (float)Layer.PlacedPieces);
                // 繪製各按鍵提示
                DrawFrame(TEX_KEYS, ControlsAnimation.AnimationPosition + new Vector2(-KEY_WIDTH, 20), 3, KEY_WIDTH, KEY_HEIGHT, (float)Layer.PlacedPieces);
                DrawFrame(TEX_KEYS, ControlsAnimation.AnimationPosition + new Vector2(CONTROLS_WIDTH, 0), 4, KEY_WIDTH, KEY_HEIGHT, (float)Layer.PlacedPieces);
                DrawFrame(TEX_KEYS, ControlsAnimation.AnimationPosition + new Vector2(CONTROLS_WIDTH, KEY_HEIGHT), 11, KEY_WIDTH, KEY_HEIGHT, (float)Layer.PlacedPieces);
                DrawFrame(TEX_KEYS, ControlsAnimation.AnimationPosition + new Vector2(-KEY_WIDTH, CONTROLS_HEIGHT - KEY_HEIGHT - 10), 10, KEY_WIDTH, KEY_HEIGHT, (float)Layer.PlacedPieces);
                DrawFrame(TEX_KEYS, ControlsAnimation.AnimationPosition + new Vector2(CONTROLS_WIDTH, CONTROLS_HEIGHT - KEY_HEIGHT - 10), 2, KEY_WIDTH, KEY_HEIGHT, (float)Layer.PlacedPieces);
            }

            // 繪製盤底與現存棋子
            for (short tile_x = 1; tile_x <= BOARD_MAX_X; tile_x++)
            {
                for (short tile_y = 1; tile_y <= BOARD_MAX_Y; tile_y++)
                {
                    // 繪製盤框
                    if (tile_x == 1)
                    {
                        DrawFrame(TEX_BORDER, BoardTile2Vector(tile_x, tile_y) + new Vector2(-BORDER_THICKNESS, 0), 3, BORDER_THICKNESS, BORDER_THICKNESS, (float)Layer.BoardBackground);
                        DrawFrame(TEX_BORDER, BoardTile2Vector(tile_x, tile_y) + new Vector2(-BORDER_THICKNESS, BORDER_THICKNESS), 3, BORDER_THICKNESS, BORDER_THICKNESS, (float)Layer.BoardBackground);
                    }
                    if (tile_x == BOARD_MAX_X)
                    {
                        DrawFrame(TEX_BORDER, BoardTile2Vector(tile_x, tile_y) + new Vector2(TILE_WIDTH, 0), 3, BORDER_THICKNESS, BORDER_THICKNESS, (float)Layer.BoardBackground, SpriteEffects.FlipHorizontally);
                        DrawFrame(TEX_BORDER, BoardTile2Vector(tile_x, tile_y) + new Vector2(TILE_WIDTH, BORDER_THICKNESS), 3, BORDER_THICKNESS, BORDER_THICKNESS, (float)Layer.BoardBackground, SpriteEffects.FlipHorizontally);
                    }
                    if (tile_y == 1)
                    {
                        DrawFrame(TEX_BORDER, BoardTile2Vector(tile_x, tile_y) + new Vector2(0, -BORDER_THICKNESS), 2, BORDER_THICKNESS, BORDER_THICKNESS, (float)Layer.BoardBackground);
                        DrawFrame(TEX_BORDER, BoardTile2Vector(tile_x, tile_y) + new Vector2(BORDER_THICKNESS, -BORDER_THICKNESS), 2, BORDER_THICKNESS, BORDER_THICKNESS, (float)Layer.BoardBackground);
                    }
                    if (tile_y == BOARD_MAX_Y)
                    {
                        DrawFrame(TEX_BORDER, BoardTile2Vector(tile_x, tile_y) + new Vector2(0, TILE_HEIGHT), 2, BORDER_THICKNESS, BORDER_THICKNESS, (float)Layer.BoardBackground, SpriteEffects.FlipVertically);
                        DrawFrame(TEX_BORDER, BoardTile2Vector(tile_x, tile_y) + new Vector2(BORDER_THICKNESS, TILE_HEIGHT), 2, BORDER_THICKNESS, BORDER_THICKNESS, (float)Layer.BoardBackground, SpriteEffects.FlipVertically);
                    }

                    switch (Board[tile_x, tile_y])
                    {
                        case 0:
                            DrawTile(0, tile_x, tile_y, Layer.BoardBackground);
                            break;
                        case 1:
                        case 2:
                            DrawTile(Board[tile_x, tile_y], tile_x, tile_y, Layer.PlacedPieces);
                            break;
                    }
                }
            }

            // 繪製盤框四角
            DrawFrame(TEX_BORDER, BoardTile2Vector(1, 1) + new Vector2(-BORDER_THICKNESS, -BORDER_THICKNESS), 1, BORDER_THICKNESS, BORDER_THICKNESS, (float)Layer.BoardBackground);
            DrawFrame(TEX_BORDER, BoardTile2Vector(BOARD_MAX_X, 1) + new Vector2(TILE_WIDTH, -BORDER_THICKNESS), 1, BORDER_THICKNESS, BORDER_THICKNESS, (float)Layer.BoardBackground, SpriteEffects.FlipHorizontally);
            DrawFrame(TEX_BORDER, BoardTile2Vector(1, BOARD_MAX_Y) + new Vector2(-BORDER_THICKNESS, TILE_HEIGHT), 1, BORDER_THICKNESS, BORDER_THICKNESS, (float)Layer.BoardBackground, SpriteEffects.FlipVertically);
            DrawFrame(TEX_BORDER, BoardTile2Vector(BOARD_MAX_X, BOARD_MAX_Y) + new Vector2(TILE_WIDTH, TILE_HEIGHT), 1, BORDER_THICKNESS, BORDER_THICKNESS, (float)Layer.BoardBackground, SpriteEffects.FlipVertically | SpriteEffects.FlipHorizontally);

            // 繪製Podium
            DrawObject(TEX_PODIUM, PODIUM_TOPLEFT_1P, Layer.Podium);
            DrawObject(TEX_PODIUM, PODIUM_TOPLEFT_2P, Layer.Podium);

            // 繪製Avatar
            DrawFrame(GetAvatarTexture(CurrentMatchState.Player[0].AvatarState), AVATAR_TOPLEFT_1P, CurrentMatchState.Player[0].AvatarCurrentFrame, AVATAR_WIDTH, AVATAR_HEIGHT, (float)Layer.AvatarAnimation, SpriteEffects.FlipHorizontally);
            DrawFrame(GetAvatarTexture(CurrentMatchState.Player[1].AvatarState), AVATAR_TOPLEFT_2P, CurrentMatchState.Player[1].AvatarCurrentFrame, AVATAR_WIDTH, AVATAR_HEIGHT, (float)Layer.AvatarAnimation);

            // 繪製集氣
            DrawObject(TEX_GAUGE_BORDER, BOMBGAUGE_TOPLEFT_1P, Layer.GaugeBorder);
            DrawObject(TEX_GAUGE_BORDER, BOMBGAUGE_TOPLEFT_2P, Layer.GaugeBorder);
            DrawMeter(TEX_GAUGE, BOMBDOT_1P, CurrentMatchState.Player[0].DisplayedBombGauge, BOMBGAUGE_MAX, COLOR_GAUGE_FILLED, COLOR_GAUGE_EMPTY, Layer.Gauge);
            DrawMeter(TEX_GAUGE, BOMBDOT_2P, CurrentMatchState.Player[1].DisplayedBombGauge, BOMBGAUGE_MAX, COLOR_GAUGE_FILLED, COLOR_GAUGE_EMPTY, Layer.Gauge);

            // 繪製Reserves
            DrawObject(TEX_RESERVES, RESERVE_TOPLEFT_1P, Layer.PoolBorder);
            DrawObject(TEX_RESERVES, RESERVE_TOPLEFT_2P, Layer.PoolBorder);
            // 繪製Next Piece
            DrawObject(TEX_NEXT, NEXT_PIECE_TOPLEFT_1P, Layer.PoolBorder);
            DrawObject(TEX_NEXT, NEXT_PIECE_TOPLEFT_2P, Layer.PoolBorder);

            // 如果Piece正在移動中就繪製動畫
            if (CurrentMatchState.PlacementAnimation.IsAnimating)
            {
                double completion = CurrentMatchState.PlacementAnimation.ElapsedDuration / CurrentMatchState.PlacementAnimation.TotalDuration;
                Vector2 xyAnimation = BoardTile2Vector(CurrentMatchState.PlacementAnimation.TileX - 1, CurrentMatchState.PlacementAnimation.TileY - 1);
                DrawPiece((short)(CurrentMatchState.CurrentPlayer + 1), CurrentMatchState.Player[CurrentMatchState.CurrentPlayer].Reserves[CurrentMatchState.ChosenPiece], xyAnimation, Layer.PlacingPiece, false, 2 - (float)completion, Convert.ToByte(completion * 255));
                bool animationComplete = CurrentMatchState.PlacementAnimation.TimerUpdate(gameTime.ElapsedGameTime.TotalMilliseconds);
            }

            // 如果有炸彈就繪製炸彈動畫
            foreach (EffectAnimation obj in BombAnimations)
            {
                if (obj.State == EffectAnimation.AnimationState.Playing)
                {
                    DrawFrame(obj.AnimationTexture, obj.AnimationPosition, obj.CurrentFrame, BOMB_WIDTH, BOMB_HEIGHT, (float)Layer.FrontEffect);
                }
            }

            // 如果有圍觀球兔就繪製圍觀球兔動畫
            foreach (Mob obj in MobAnimations)
            {
                Texture2D texture;
                switch (obj.MobState)
                {
                    case Mob.MobStates.Hopping:
                        texture = TEX_MOB_HOP;
                        break;
                    case Mob.MobStates.Turning:
                        texture = TEX_MOB_TURN;
                        break;
                    default:
                        texture = TEX_MOB_IDLE;
                        break;
                }
                SpriteEffects flip;
                if (obj.LeftEntry) flip = SpriteEffects.None; else flip = SpriteEffects.FlipHorizontally;
                DrawFrame(texture, new Vector2(obj.CurrentX, obj.TargetY), obj.CurrentFrame, MOB_WIDTH, MOB_HEIGHT, (float)Layer.Mobs - obj.Scale, flip, obj.Scale);
            }

            Vector2 xyStart;
            // 繪製選取中Reserves的背景
            // 1P側
            xyStart = ChosenReservePosition();
            DrawFill(TEX_TILE, 3, new Rectangle((int)xyStart.X, (int)xyStart.Y, TILE_WIDTH * 4, TILE_HEIGHT * 4), Color.Blue, Layer.PoolSelection);

            // 繪製上面的Piece
            // Reserve 1的左端是Reserves欄左起2格，頂端是上起2格；每個Piece都有3格寬 + 與下一個Piece之間的緩衝1格
            xyStart = RESERVE_TOPLEFT_1P + new Vector2(TILE_WIDTH * 2, TILE_HEIGHT * 2);
            for (short i = 0; i < 3; i++)
            {
                // 繪製代表Reserve的按鍵
                DrawFrame(TEX_KEYS, xyStart + new Vector2(TILE_WIDTH * 1.5f - KEY_WIDTH * .5f, -TILE_HEIGHT * 2), (short)(i + 5), KEY_WIDTH, KEY_HEIGHT, (float)Layer.PlacedPieces);

                // 不繪製正在移動的Piece
                if (!CurrentMatchState.PlacementAnimation.IsAnimating || CurrentMatchState.ChosenPiece != i || CurrentMatchState.CurrentPlayer != 0)
                {
                    DrawPiece(1, CurrentMatchState.Player[0].Reserves[i], xyStart, Layer.PlacedPieces);
                }

                // 移動到下一個Piece的起點位置
                xyStart.X += TILE_WIDTH * 4;
            }
            // 換2P
            xyStart = RESERVE_TOPLEFT_2P + new Vector2(TILE_WIDTH * 2, TILE_HEIGHT * 2);
            for (short i = 0; i < 3; i++)
            {
                // 繪製代表Reserve的按鍵
                DrawFrame(TEX_KEYS, xyStart + new Vector2(TILE_WIDTH * 1.5f - KEY_WIDTH * .5f, -TILE_HEIGHT * 2), (short)(i + 5), KEY_WIDTH, KEY_HEIGHT, (float)Layer.PlacedPieces);

                // 不繪製正在移動的Piece
                if (!CurrentMatchState.PlacementAnimation.IsAnimating || CurrentMatchState.ChosenPiece != i || CurrentMatchState.CurrentPlayer != 1)
                {
                    DrawPiece(2, CurrentMatchState.Player[1].Reserves[i], xyStart, Layer.PlacedPieces);
                }

                // 移動到下一個Piece的起點位置
                xyStart.X += TILE_WIDTH * 4;
            }

            // Next Piece的左端是Next Piece欄左起1.5格，頂端是上起2格
            xyStart = NextPiecePosition(0);
            DrawPiece(1, CurrentMatchState.Player[0].NextPiece, xyStart, Layer.PlacedPieces);
            xyStart = NextPiecePosition(1);
            DrawPiece(2, CurrentMatchState.Player[1].NextPiece, xyStart, Layer.PlacedPieces);

            // 依照選定的Piece類別繪製游標
            // 判斷是否可操作＆游標是否在盤面內
            if (CurrentMatchState.Phase == MatchPhase.Placement && CurrentMatchState.IsPointingTile)
            {
                Vector2 xyPointed = BoardTile2Vector(CurrentMatchState.PointedTileX - 1, CurrentMatchState.PointedTileY - 1);
                DrawPiece((short)(CurrentMatchState.CurrentPlayer + 1), CurrentMatchState.CurrentPieceShape, xyPointed, Layer.PiecePreview, true);
            }
        }

        /// <summary>
        /// 繪製整張Texture
        /// </summary>
        /// <param name="texture">要繪製的Texture</param>
        /// <param name="xy">繪製座標</param>
        /// <param name="drawLayer">繪製圖層</param>
        /// <param name="flip">如何翻轉</param>
        void DrawObject(Texture2D texture, Vector2 xy, Layer drawLayer, SpriteEffects flip = SpriteEffects.None)
        {
            spriteBatch.Draw(texture, xy, null, Color.White, 0f, Vector2.Zero, 1f, flip, (float)drawLayer / (float)Layer.MAX);
        }
        /// <summary>
        /// Draw指定texture的指定frame
        /// </summary>
        void DrawFrame(Texture2D texture, Vector2 xy, short frame, int frameWidth, int frameHeight, float drawLayer, SpriteEffects flip = SpriteEffects.None, float scale = 1f)
        {
            Rectangle rect;
            int frameRow = 0, frameCol;
            // frame超過texture總寬度則換行
            frameCol = (frame - 1) * frameWidth;
            while (frameCol >= texture.Width)
            {
                frameCol -= texture.Width;
                frameRow += frameHeight;
            }

            rect = new Rectangle(frameCol, frameRow, frameWidth, frameHeight);
            spriteBatch.Draw(texture, xy, rect, Color.White, 0f, Vector2.Zero, scale, flip, drawLayer / (float)Layer.MAX);
        }
        void DrawTile(short tileNo, short tile_x, short tile_y, Layer drawLayer)
        {
            spriteBatch.Draw(TEX_TILE, BoardTile2Vector(tile_x, tile_y), new Rectangle(tileNo * 32, 0, TILE_WIDTH, TILE_HEIGHT), Color.White, 0f, Vector2.Zero, 1f, SpriteEffects.None, (float)drawLayer / (float)Layer.MAX);
        }
        void DrawTile(short tileNo, short tile_x, short tile_y, Color drawColor, Layer drawLayer)
        {
            spriteBatch.Draw(TEX_TILE, BoardTile2Vector(tile_x, tile_y), new Rectangle(tileNo * 32, 0, TILE_WIDTH, TILE_HEIGHT), drawColor, 0f, Vector2.Zero, 1f, SpriteEffects.None, (float)drawLayer / (float)Layer.MAX);
        }
        void DrawMeter(Texture2D texture, Rectangle scope, float num, float max, Color currentColor, Color maxColor, Layer drawLayer)
        {
            Rectangle currentScope = scope, maxScope = scope;
            currentScope.Width = (int)Math.Floor(scope.Width * (num / max));
            maxScope.Width = (int)Math.Floor(scope.Width * (1 - num / max));
            maxScope.X += currentScope.Width;
            spriteBatch.Draw(texture, currentScope, null, currentColor, 0f, Vector2.Zero, SpriteEffects.None, (float)drawLayer / (float)Layer.MAX);
            spriteBatch.Draw(texture, maxScope, null, maxColor, 0f, Vector2.Zero, SpriteEffects.None, (float)drawLayer / (float)Layer.MAX);
        }
        /// <summary>
        /// 繪製Piece
        /// </summary>
        void DrawPiece(short tileNo, PieceShape pieceShape, Vector2 xy, Layer drawLayer, bool isPreview = false, float scale = 1f, byte alpha = 255)
        {
            Color colorTile = Color.White;
            colorTile.A = alpha;
            if (isPreview)
            {
                // 改成游標顏色
                colorTile = COLOR_PLACEABLE;
                if (!IsCurrentPiecePlaceable())
                {
                    colorTile = COLOR_UNPLACEABLE;
                    tileNo = 3;
                }
            }

            Vector2 xyOffset = new Vector2(TILE_WIDTH * .5f * scale, TILE_HEIGHT * .5f * scale);
            Vector2 xyPieceCenter = xy + new Vector2(TILE_WIDTH, TILE_HEIGHT);

            for (short y = 0; y < 3; y++)
            {
                for (short x = 0; x < 3; x++)
                {
                    // 根據PieceType判斷是否要畫上Tile
                    if (pieceShape.GetTile(8 - y * 3 - x))
                    {
                        Vector2 xyTarget = xyPieceCenter + new Vector2((x - 1) * TILE_WIDTH * scale, (y - 1) * TILE_HEIGHT * scale);
                        Vector2 tilePointed = Vector2BoardTile(xyTarget);
                        // 如果是游標，則再判斷該Tile是否在盤面內
                        if (!isPreview || (tilePointed.X > 0 && tilePointed.X <= BOARD_MAX_X && tilePointed.Y > 0 && tilePointed.Y <= BOARD_MAX_Y))
                        {
                            spriteBatch.Draw(TEX_TILE, xyTarget + xyOffset, new Rectangle(tileNo * 32, 0, TILE_WIDTH, TILE_HEIGHT), colorTile, 0f, xyOffset, scale, SpriteEffects.None, (float)drawLayer / (float)Layer.MAX);
                        }
                    }
                }
            }
        }
        /// <summary>
        /// 在指定Rectangle範圍內反覆Draw指定的tile直到填滿。目前沒有辦法把超出範圍部分切掉，不過也用不到所以暫時不管
        /// </summary>
        void DrawFill(Texture2D texture, short tileNo, Rectangle drawRegion, Color colorFill, Layer drawLayer, SpriteEffects flip = SpriteEffects.None)
        {
            int tilexRepeat, tileyRepeat;
            tilexRepeat = (int)Math.Ceiling(drawRegion.Width / TILE_WIDTH * 1f);
            tileyRepeat = (int)Math.Ceiling(drawRegion.Height / TILE_HEIGHT * 1f);
            for (int x = 0; x < tilexRepeat; x++)
            {
                for (int y = 0; y < tileyRepeat; y++)
                {
                    spriteBatch.Draw(texture, new Vector2(drawRegion.X + x * TILE_WIDTH, drawRegion.Y + y * TILE_HEIGHT), new Rectangle(tileNo * 32, 0, TILE_WIDTH, TILE_HEIGHT), colorFill, 0, Vector2.Zero, 1f, flip, (float)drawLayer / (float)Layer.MAX);
                }
            }
        }
        Texture2D GetAvatarTexture(MatchState.PlayerState.AnimationState state)
        {
            switch (state)
            {
                case MatchState.PlayerState.AnimationState.Dead:
                    return TEX_AVATAR_DEAD;
                case MatchState.PlayerState.AnimationState.Fall:
                    return TEX_AVATAR_FALL;
                case MatchState.PlayerState.AnimationState.Victory:
                    return TEX_AVATAR_VICTORY;
                default:
                    return TEX_AVATAR_IDLE;
            }
        }
        /// <summary>
        /// 傳回目前選定Reserve的座標。Reserve 1的左端是Reserves欄左起1.5格，頂端是上起1.5格；每塊背景都有4格寬4格高（比Piece上下左右各多0.5格）
        /// </summary>
        /// <returns></returns>
        Vector2 ChosenReservePosition()
        {
            Vector2 xyStart = Vector2.Zero;
            if (CurrentMatchState.CurrentPlayer == 0) xyStart = RESERVE_TOPLEFT_1P;
            if (CurrentMatchState.CurrentPlayer == 1) xyStart = RESERVE_TOPLEFT_2P;
            xyStart += new Vector2(TILE_WIDTH * 1.5f + CurrentMatchState.ChosenPiece * TILE_WIDTH * 4f, TILE_HEIGHT * 1.5f);
            return xyStart;
        }

        Vector2 NextPiecePosition(short playerno)
        {
            Vector2 xyStart;
            if (playerno == 0) xyStart = NEXT_PIECE_TOPLEFT_1P; else xyStart = NEXT_PIECE_TOPLEFT_2P;
            xyStart += new Vector2(TILE_WIDTH * 1.5f, TILE_HEIGHT * 2f);
            return xyStart;
        }
    }
}