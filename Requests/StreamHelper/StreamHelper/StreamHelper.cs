using System;
using System.Drawing;
using System.Linq;
using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;
using Color = System.Drawing.Color;

namespace StreamHelper
{
    internal class StreamHelper
    {
        private static Obj_AI_Hero _player = ObjectManager.Player;
        private static Vector3 _actPosition, _newPosition, _offsetPosition;
        private static int _lastClickTime, _newClickTime, _movetoDisplay, _cursorPos;
        private static Random _rnd = new Random();
        private static Render.Sprite _cursorAttack, _cursorMove, _moveTo;
        private static Menu _menu;
        private static float _cursorPosRef;
        private static bool _idle;

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
            MoveTo.AddItem(new MenuItem("InfoI", "It won't work wit the common fakeClick"));
            _menu.AddSubMenu(MoveTo);

            _menu.AddItem(new MenuItem("Debug", "Debug")).SetValue(false);
            _menu.AddItem(new MenuItem("Enabled", "Enabled")).SetValue(true);
            _menu.AddItem(new MenuItem("Speed", "Speed")).SetValue(new Slider(150, 90, 250));
            _menu.AddItem(new MenuItem("Linear", "Linear cursor speed")).SetValue(false);
            _menu.AddToMainMenu();
            _cursorAttack = new Render.Sprite(
                Properties.Resources.Attack, new Vector2((Drawing.Width / 2), (Drawing.Height / 2)));
            _cursorAttack.Add(0);
            _cursorAttack.Visible = false;
            _cursorAttack.OnDraw();

            _cursorMove = new Render.Sprite(
                Properties.Resources.Normal, new Vector2((Drawing.Width / 2), (Drawing.Height / 2)));
            _cursorMove.Add(0);
            _cursorMove.Visible = false;
            _cursorMove.OnDraw();

            _moveTo = new Render.Sprite(
                Properties.Resources.MoveTo, new Vector2((Drawing.Width / 2), (Drawing.Height / 2)));
            _moveTo.Add(0);
            _moveTo.Visible = false;
            _moveTo.OnDraw();

            _newPosition = Game.CursorPos;
            _actPosition = Game.CursorPos;
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
            _idle = false;
            if (!_menu.Item("Enabled").GetValue<bool>())
            {
                _cursorAttack.Visible = false;
                _cursorMove.Visible = false;
                return;
            }
            if (_offsetPosition.IsValid() && _offsetPosition.Distance(_newPosition) < Speed(100))
            {
                _offsetPosition = Vector3.Zero;
            }
            else if (_offsetPosition.IsValid())
            {
                _offsetPosition = _offsetPosition.Extend(_newPosition, Speed(_offsetPosition.Distance(_newPosition)));
            }
            else
            {
                _offsetPosition = Vector3.Zero;
            }
            SetActPos();
            var finalPos = _actPosition;
            if (!IsThereUnit(_newPosition))
            {
                _idle = true;
                _newPosition = Game.CursorPos;
            }
            MoveCursors(finalPos);
            if (_actPosition.Distance(_newPosition) < 1)
            {
                _movetoDisplay = Environment.TickCount + 150;
            }
            if (IsThereUnit(finalPos))
            {
                _cursorAttack.Visible = true;
                _cursorMove.Visible = false;
                _moveTo.Visible = false;
            }
            else if (MoveToCursorEnabled() && Game.CursorPos.Distance(_newPosition) < 50 &&
                     Game.CursorPos.Distance(_actPosition) < 150 && Game.CursorPos.Distance(_actPosition) > 50 &&
                     _movetoDisplay - Environment.TickCount <= 150)
            {
                _cursorAttack.Visible = false;
                _cursorMove.Visible = false;
                _moveTo.Visible = true;
            }
            else
            {
                _cursorAttack.Visible = false;
                _cursorMove.Visible = true;
                _moveTo.Visible = false;
            }
        }

        private bool IsThereUnit(Vector3 pos)
        {
            var heroesUnderCursor =
                HeroManager.Enemies.FirstOrDefault(h => h.IsValidTarget() && h.Distance(pos) < h.BoundingRadius + 50);
            var minionUnderCursor =
                MinionManager.GetMinions(pos, 600, MinionTypes.All, MinionTeam.NotAlly)
                    .FirstOrDefault(m => m.IsValidTarget() && m.Distance(pos) < m.BoundingRadius + 50);
            var obj =
                ObjectManager.Get<Obj_AI_Base>()
                    .FirstOrDefault(m => m.IsValidTarget() && m.Distance(pos) < m.BoundingRadius + 50);
            return obj != null;
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
            var dSpeed = Speed(l);
            if (l < 70)
            {
                _actPosition = _newPosition;
            }
            _actPosition = _actPosition.Extend(_offsetPosition.IsValid() ? _offsetPosition : _newPosition, dSpeed);
        }

        private static float Speed(float l)
        {
            var speed = _menu.Item("Speed").GetValue<Slider>().Value;
            var mod = 1f;
            if (!_menu.Item("Linear").GetValue<bool>())
            {
                mod = Math.Max((l / 150), 1);
            }

            var dSpeed = 35 * (speed / 100f) * mod;

            var better = 0f;
            better = ((dSpeed * Math.Min((((1f / _player.AttackDelay)) + _player.AttackCastDelay), 1f)));
            better = Math.Min(better, l);
            return better;
        }

        private void MoveCursors(Vector3 pos)
        {
            if (MenuGUI.IsShopOpen || (_idle && Environment.TickCount - _lastClickTime > 1600))
            {
                _cursorMove.Position = Utils.GetCursorPos();
                _cursorAttack.Position = Utils.GetCursorPos();
                _moveTo.Position = Utils.GetCursorPos();
            }
            else
            {
                _cursorMove.Position = Drawing.WorldToScreen(pos);
                _cursorAttack.Position = Drawing.WorldToScreen(pos);
                _moveTo.Position = Drawing.WorldToScreen(pos);
            }
        }

        private void Drawing_OnDraw(EventArgs args)
        {
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
                Render.Circle.DrawCircle(_offsetPosition, 70, Color.Blue, 7);
            }
        }

        private void Obj_AI_Base_OnProcessSpellCast(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            if (sender.IsMe)
            {
                if ((args.Target != null && args.Target.IsMe) ||
                    args.SData.TargettingType.ToString().ToLower().Contains("self"))
                {
                    return;
                }
                SetNewPos(args.End, !args.SData.IsAutoAttack(), (IsThereUnit(args.End) || (args.Target != null)));
            }
        }

        private void Obj_AI_Hero_OnIssueOrder(Obj_AI_Base sender, GameObjectIssueOrderEventArgs args)
        {
            if (sender.IsMe && args.Order != GameObjectOrder.HoldPosition && args.Order != GameObjectOrder.Stop)
            {
                //SetNewPos(args.TargetPosition);
            }
        }

        private void SetNewPos(Vector3 pos, bool isSpell = false, bool hasTarget = false)
        {
            var distance = (int) _player.Distance(pos);
            // HoldPosition
            if ((distance < 1 && !isSpell) || !pos.IsOnScreen())
            {
                return;
            }
            var closerPos = _player.Position.Extend(pos, _rnd.Next(300, 600));
            if (distance > 300 && isSpell && !hasTarget)
            {
                pos = closerPos;
                distance = (int) _player.Distance(pos);
            }
            pos = new Vector3(
                pos.X + _rnd.Next(-50, 50),
                pos.Y + (HeroManager.Enemies.Any(e => e.Distance(pos) < 130) ? 130 : _rnd.Next(10, 50)), pos.Z);

            var between = _player.Position.Extend(pos, distance / 2);
            var offRad = Math.Max(distance / 2, 100);
            var off = new Vector3(
                between.X + _rnd.Next(-offRad, offRad), between.Y + +_rnd.Next(-offRad, offRad), between.Z);

            if (pos.Distance(Game.CursorPos) < 100)
            {
                off = Vector3.Zero;
            }
            _lastClickTime = _newClickTime;
            _newPosition = pos;
            _offsetPosition = off;
            _newClickTime = Environment.TickCount;
        }
    }
}