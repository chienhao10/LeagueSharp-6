using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net.NetworkInformation;
using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;
using StreamHelper.Properties;
using Color = System.Drawing.Color;

namespace StreamHelper
{
    internal class StreamHelper
    {
        private static Obj_AI_Hero _player = ObjectManager.Player;
        private static Vector3 _actPosition, _newPosition, _offsetPosition, _dbg;
        private static int _lastClickTime, _newClickTime, _movetoDisplay, _cursorPos;
        private static Random _rnd = new Random();
        private static List<CursorSprite> _cursors = new List<CursorSprite>();
        private static Menu _menu;
        private static float _cursorPosRef, _speed, _lastUpdate;
        private static bool _idle;
        private static Vector3 _lastTargetPos, _lastTargetPosPerm, _lastPlayerPos;

        public StreamHelper()
        {
            Obj_AI_Base.OnProcessSpellCast += Obj_AI_Base_OnProcessSpellCast;
            Obj_AI_Hero.OnIssueOrder += Obj_AI_Hero_OnIssueOrder;
            Drawing.OnDraw += Drawing_OnDraw;
            Game.OnUpdate += Game_OnUpdate;

            _menu = new Menu("StreamHelper", "StreamHelper", true);
            Menu MoveTo = new Menu("Moveto Cursor", "Moveto");
            MoveTo.AddItem(new MenuItem("MovetoLasthit", "Lasthit"))
                .SetValue(new KeyBind("X".ToCharArray()[0], KeyBindType.Press))
                .SetFontStyle(FontStyle.Bold, SharpDX.Color.Orange);
            MoveTo.AddItem(new MenuItem("MovetoMixed", "Mixed"))
                .SetValue(new KeyBind("C".ToCharArray()[0], KeyBindType.Press))
                .SetFontStyle(FontStyle.Bold, SharpDX.Color.Orange);
            MoveTo.AddItem(new MenuItem("MovetoClear", "Clear"))
                .SetValue(new KeyBind("A".ToCharArray()[0], KeyBindType.Press))
                .SetFontStyle(FontStyle.Bold, SharpDX.Color.Orange);
            MoveTo.AddItem(new MenuItem("MovetoCombo", "Combo"))
                .SetValue(new KeyBind(32, KeyBindType.Press))
                .SetFontStyle(FontStyle.Bold, SharpDX.Color.Orange);
            MoveTo.AddItem(new MenuItem("MovetoEnable", "Enable")).SetValue(false);
            MoveTo.AddItem(new MenuItem("MovetoOnlyEnemy", "Only around enemies")).SetValue(true);
            MoveTo.AddItem(new MenuItem("MovetoAACircle", "Draw AA circle")).SetValue(false);
            _menu.AddSubMenu(MoveTo);

            _menu.AddItem(new MenuItem("Debug", "Debug")).SetValue(false);
            _menu.AddItem(new MenuItem("Enabled", "Enabled")).SetValue(true);
            _menu.AddItem(new MenuItem("Speed", "Speed")).SetValue(new Slider(180, 60, 500));
            _menu.AddItem(new MenuItem("Linear", "Linear cursor speed")).SetValue(false);
            _menu.AddItem(new MenuItem("Colorblind", "Colorblind mode")).SetValue(false);
            _menu.AddToMainMenu();

            _cursors.Add(new CursorSprite(Cursors.Attack, Resources.Attack, Resources.Attack_CB));
            _cursors.Add(new CursorSprite(Cursors.Normal, Resources.Normal));
            _cursors.Add(new CursorSprite(Cursors.MoveTo, Resources.MoveTo, Resources.MoveTo_CB));
            _cursors.Add(new CursorSprite(Cursors.Shop, Resources.Shop));
            _cursors.Add(new CursorSprite(Cursors.Turret, Resources.Turret));

            _newPosition = Game.CursorPos;
            _actPosition = Game.CursorPos;
            _speed = 500;
        }


        private bool MoveToCursorEnabled()
        {
            if (!_menu.Item("MovetoEnable").GetValue<bool>())
            {
                return false;
            }
            if (_menu.Item("MovetoOnlyEnemy").GetValue<bool>() && !IsThereUnitPlayer())
            {
                return false;
            }
            if (_menu.Item("MovetoLasthit").GetValue<KeyBind>().Active)
            {
                return true;
            }
            if (_menu.Item("MovetoMixed").GetValue<KeyBind>().Active)
            {
                return true;
            }
            if (_menu.Item("MovetoClear").GetValue<KeyBind>().Active)
            {
                return true;
            }
            if (_menu.Item("MovetoCombo").GetValue<KeyBind>().Active)
            {
                return true;
            }
            return false;
        }

        private void Game_OnUpdate(EventArgs args)
        {
            if (_lastTargetPos.IsValid())
            {
                _lastTargetPosPerm = _lastTargetPos;
            }
            var currentCursor = Cursors.Normal;
            if (IsThereUnit(_actPosition, true))
            {
                currentCursor = Cursors.Attack;
            }
            else if (MoveToCursorEnabled() && Game.CursorPos.Distance(_newPosition) < 50 &&
                     Game.CursorPos.Distance(_actPosition) < 250 && Game.CursorPos.Distance(_actPosition) > 50 &&
                     _movetoDisplay - Environment.TickCount <= 150)
            {
                currentCursor = Cursors.MoveTo;
            }
            else
            {
                currentCursor = Cursors.Normal;
            }
            if ((!CursorAtMiddle && !GameCursorAtMiddle))
            {
                currentCursor = Cursors.None;
            }

            if (IsThereShop(_actPosition))
            {
                currentCursor = Cursors.Shop;
            }
            if (IsThereAlly(_actPosition))
            {
                currentCursor = Cursors.Turret;
            }
            SetCursorType(currentCursor);
            if (!_menu.Item("Enabled").GetValue<bool>())
            {
                SetCursorType(Cursors.None);
                return;
            }
            if (Environment.TickCount - _lastUpdate > 12 || (!IsThereUnit(_newPosition) && !_lastTargetPos.IsValid()))
            {
                _lastUpdate = Environment.TickCount;
            }
            else
            {
                return;
            }
            _idle = false;
            if (_offsetPosition.IsValid() && _offsetPosition.Distance(_newPosition) < 70)
            {
                _offsetPosition = Vector3.Zero;
            }
            else if (_offsetPosition.IsValid())
            {
                var s = _speed;
                if (_offsetPosition.Distance(_newPosition) < s)
                {
                    s = _offsetPosition.Distance(_newPosition) / 2;
                }
                _offsetPosition = _offsetPosition.Extend(_newPosition, s);
            }
            else
            {
                _offsetPosition = Vector3.Zero;
            }
            if (_actPosition.Distance(Game.CursorPos) < 50 && _actPosition.Distance(_lastTargetPos) > 200 &&
                _newPosition.Distance(Game.CursorPos) < 50)
            {
                _lastTargetPos = Vector3.Zero;
            }
            SetActPos();
            var finalPos = _actPosition;
            if (!IsThereUnit(_newPosition) && !_lastTargetPos.IsValid())
            {
                _idle = true;
                _newPosition = Vector3.Zero;
                _speed = 500;
            }
            MoveCursors(finalPos);
            if (_actPosition.Distance(_newPosition) < 1)
            {
                _movetoDisplay = Environment.TickCount + 150;
            }
        }

        private void SetCursorType(Cursors currentCursor)
        {
            foreach (var cursor in _cursors)
            {
                if (cursor.Type == currentCursor)
                {
                    cursor.Enabled(true, _menu.Item("Colorblind").GetValue<bool>());
                }
                else
                {
                    cursor.Enabled(false, _menu.Item("Colorblind").GetValue<bool>());
                }
            }
        }

        private void SetCursorPos(Vector2 pos)
        {
            foreach (var cursor in _cursors)
            {
                cursor.SetPosition(pos);
            }
        }

        private bool IsThereUnit(Vector3 pos, bool cursor = false)
        {
            if (_lastTargetPos.IsValid() && cursor && _lastTargetPos.Distance(pos) < 150)
            {
                return true;
            }
            var obj =
                ObjectManager.Get<Obj_AI_Base>()
                    .Count(m => !m.IsAlly && m.Distance(pos) < 150 && m.IsValidTarget() && m.Health > 0);
            return obj > 0;
        }

        private bool IsThereUnitPlayer()
        {
            var heroesUnderCursor =
                HeroManager.Enemies.FirstOrDefault(h => h.IsValidTarget() && h.Distance(_player.Position) < 1000);
            var minionUnderCursor =
                MinionManager.GetMinions(_player.Position, 1000, MinionTypes.All, MinionTeam.NotAlly)
                    .FirstOrDefault(m => m.IsValidTarget());
            var obj =
                ObjectManager.Get<Obj_AI_Base>()
                    .FirstOrDefault(m => m.IsValidTarget() && m.Distance(_player.Position) < 1000);
            return obj != null;
        }

        public static bool IsThereShop(Vector3 position)
        {
            var shop = ObjectManager.Get<Obj_Shop>().FirstOrDefault(o => o.Position.Distance(position) < 300);
            return ObjectManager.Player.InShop() && shop != null && !MenuGUI.IsShopOpen;
        }

        public static bool IsThereAlly(Vector3 position)
        {
            var allyTurret =
                ObjectManager.Get<Obj_AI_Turret>().Count(o => o.IsAlly && o.Distance(position) < 120 && o.Health > 0);
            var ally = HeroManager.Allies.Count(m => m.Distance(position) < 120 && !m.IsMe && m.Health > 0);
            return allyTurret + ally > 0;
        }

        private void SetActPos()
        {
            if (_actPosition == _newPosition)
            {
                _newPosition = Game.CursorPos;
                return;
            }
            if (_actPosition.Distance(_newPosition) > 3000)
            {
                _newPosition = Game.CursorPos;
                _actPosition = Game.CursorPos;
                return;
            }
            var l = _actPosition.Distance(_newPosition);
            var dSpeed = _speed;
            if (l < dSpeed)
            {
                dSpeed = l / 2;
            }
            if (l < 70)
            {
                _actPosition = _newPosition;
                return;
            }
            _actPosition = _actPosition.Extend(_offsetPosition.IsValid() ? _offsetPosition : _newPosition, dSpeed);
        }

        private static float Speed(float l)
        {
            var speed = _menu.Item("Speed").GetValue<Slider>().Value;
            var mod = 2f;
            if (!_menu.Item("Linear").GetValue<bool>())
            {
                mod = Math.Max((l / 250), 1);
            }

            var dSpeed = 35 * (speed / 100f) * mod;

            var better = 0f;
            better = ((dSpeed * Math.Min((((1f / _player.AttackDelay)) + _player.AttackCastDelay), 1f)));
            better = Math.Min(better, l);
            return better;
        }

        private void MoveCursors(Vector3 pos)
        {
            if (MenuGUI.IsShopOpen || (_idle && Environment.TickCount - _lastClickTime > _speed * 2f && CursorAtMiddle))
            {
                SetCursorPos(Utils.GetCursorPos());
            }
            else
            {
                SetCursorPos(Drawing.WorldToScreen(pos));
            }
        }

        private bool CursorAtMiddle
        {
            get { return Utils.GetCursorPos().Distance(new Vector2((Drawing.Width / 2), (Drawing.Height / 2))) > 60; }
        }

        private bool GameCursorAtMiddle
        {
            get
            {
                return
                    Drawing.WorldToScreen(Game.CursorPos)
                        .Distance(new Vector2((Drawing.Width / 2), (Drawing.Height / 2))) < 60;
            }
        }

        private void Drawing_OnDraw(EventArgs args)
        {
            if (_menu.Item("MovetoAACircle").GetValue<bool>() && MoveToCursorEnabled() &&
                IsThereUnit(_newPosition, true))
            {
                Drawing.DrawCircle(_player.Position, Orbwalking.GetRealAutoAttackRange(null), Color.Cyan);
                Render.Circle.DrawCircle(_player.Position, Orbwalking.GetRealAutoAttackRange(null), Color.Honeydew, 8);
            }
            if (!_menu.Item("Debug").GetValue<bool>())
            {
                return;
            }
            if (_actPosition.IsValid())
            {
                Render.Circle.DrawCircle(_actPosition, 60, Color.Red, 7);
            }
            if (_newPosition.IsValid())
            {
                Render.Circle.DrawCircle(_newPosition, 70, Color.LawnGreen, 7);
            }
            if (_offsetPosition.IsValid())
            {
                Render.Circle.DrawCircle(_offsetPosition, 80, Color.Blue, 7);
            }
            if (_dbg.IsValid())
            {
                Render.Circle.DrawCircle(_dbg, 80, Color.DarkCyan, 7);
            }
            if (_lastTargetPos.IsValid())
            {
                Render.Circle.DrawCircle(_lastTargetPos, 50, Color.Violet, 7);
            }
        }

        private void Obj_AI_Base_OnProcessSpellCast(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            if (sender.IsMe)
            {
                if (args.SData.IsAutoAttack() && args.SData.Name.ToLower().Contains("ghoul"))
                {
                    return;
                }
                if ((args.Target != null && args.Target.IsMe) ||
                    args.SData.TargettingType.ToString().ToLower().Contains("self"))
                {
                    return;
                }
                SetNewPos(args.End, !args.SData.IsAutoAttack(), (args.Target != null || IsThereUnit(args.End)));
            }
        }

        private void Obj_AI_Hero_OnIssueOrder(Obj_AI_Base sender, GameObjectIssueOrderEventArgs args)
        {
            if (sender.IsMe && args.Order != GameObjectOrder.HoldPosition && args.Order != GameObjectOrder.Stop)
            {
                //SetNewPos(args.TargetPosition);
            }
        }

        private void SetNewPos(Vector3 pos, bool isSpell, bool target)
        {
            var distance = (int) _player.Distance(pos);
            // HoldPosition
            if ((distance < 1 && !isSpell) || !pos.IsOnScreen())
            {
                return;
            }
            if (pos.Distance(_lastTargetPosPerm) < 50 && _lastPlayerPos == _player.Position)
            {
                return;
            }
            if ((_actPosition.IsValid() && _actPosition.Distance(pos) < 150) || Game.CursorPos.Distance(pos) < 150)
            {
                return;
            }
            var closerPos = _player.Position.Extend(pos, _rnd.Next(300, 600));
            if (distance > 300 && isSpell && !target)
            {
                //Console.WriteLine("Reduced cucc");
                //pos = closerPos;
                //distance = (int) _player.Distance(pos);
            }
            pos = new Vector3(
                pos.X + _rnd.Next(-50, 50),
                pos.Y + (HeroManager.Enemies.Any(e => e.Distance(pos) < 130) ? 130 : _rnd.Next(10, 50)), pos.Z);

            var between = Game.CursorPos.Extend(pos, distance / 2f);
            var offRad = Math.Max(distance / 2, 100);
            var off = new Vector3(
                between.X + _rnd.Next(-offRad, offRad), between.Y + +_rnd.Next(-offRad, offRad), between.Z);

            if (pos.Distance(Game.CursorPos) < 100)
            {
                //Console.WriteLine("Null");
                off = Vector3.Zero;
            }
            _lastClickTime = _newClickTime;
            _newPosition = pos;
            _offsetPosition = off;
            _dbg = off;
            _newClickTime = Environment.TickCount;
            _lastTargetPos = pos;
            _actPosition = Game.CursorPos;
            _speed = Speed(Game.CursorPos.Distance(_newPosition));
            _lastPlayerPos = _player.Position;
        }
    }
}