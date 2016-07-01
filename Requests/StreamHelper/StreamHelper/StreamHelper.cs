using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;
using Color = System.Drawing.Color;

namespace StreamHelper
{
    internal class StreamHelper
    {
        private static Obj_AI_Hero _player = ObjectManager.Player;
        private static Vector3 _actPosition;
        private static Vector3 _newPosition;
        private static int _lastClickTime;
        private static int _newClickTime;
        private static Random _rnd = new Random();
        private static Render.Sprite _cursorAttack, _cursorMove;
        private static Menu _menu;

        public StreamHelper()
        {
            Obj_AI_Base.OnProcessSpellCast += Obj_AI_Base_OnProcessSpellCast;
            Obj_AI_Hero.OnIssueOrder += Obj_AI_Hero_OnIssueOrder;
            Drawing.OnDraw += Drawing_OnDraw;
            Game.OnUpdate += Game_OnUpdate;

            _menu = new Menu("StreamHelper", "StreamHelper", true);
            _menu.AddItem(new MenuItem("Debug", "Debug")).SetValue(false);
            _menu.AddItem(new MenuItem("Enabled", "Enabled")).SetValue(true);
            _menu.AddItem(new MenuItem("Speed", "Speed")).SetValue(new Slider(150, 90, 250));
            _menu.AddToMainMenu();
            _cursorAttack = new Render.Sprite(
                Properties.Resources.Attack, new Vector2((Drawing.Width / 2), (Drawing.Height / 2)));
            _cursorAttack.Add(0);
            _cursorAttack.Visible = false;
            _cursorAttack.OnDraw();

            _cursorMove = new Render.Sprite(
                Properties.Resources.Normal, new Vector2((Drawing.Width / 2), (Drawing.Height / 2)));
            _cursorMove.Add(0);
            _cursorMove.OnDraw();

            _newPosition = Game.CursorPos;
            _actPosition = Game.CursorPos;
        }

        private void Obj_AI_Hero_OnIssueOrder(Obj_AI_Base sender, GameObjectIssueOrderEventArgs args)
        {
            if (sender.IsMe && args.Order != GameObjectOrder.HoldPosition && args.Order != GameObjectOrder.Stop)
            {
                SetNewPos(args.TargetPosition);
            }
        }

        private void Game_OnUpdate(EventArgs args)
        {
            if (!_menu.Item("Enabled").GetValue<bool>())
            {
                _cursorAttack.Visible = false;
                _cursorMove.Visible = false;
                return;
            }
            SetActPos();
            var finalPos = _actPosition;
            if (!IsThereUnit(_newPosition))
            {
                _newPosition = Game.CursorPos;
            }
            MoveCursors(finalPos);

            if (IsThereUnit(finalPos))
            {
                _cursorAttack.Visible = true;
                _cursorMove.Visible = false;
            }
            else
            {
                _cursorAttack.Visible = false;
                _cursorMove.Visible = true;
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

        private void SetActPos()
        {
            if (_actPosition == _newPosition)
            {
                _newPosition = Game.CursorPos;
                return;
            }
            if (_actPosition.Distance(_newPosition) > 2000)
            {
                _newPosition = Game.CursorPos;
                _actPosition = Game.CursorPos;
                return;
            }
            var l = _actPosition.Distance(_newPosition);
            var t = _newClickTime - _lastClickTime;
            var deltaT = (float) (Environment.TickCount) / (float) (_newClickTime + 1000f);
            var speed = _menu.Item("Speed").GetValue<Slider>().Value;
            var lerp = MathUtil.Lerp(0, l, Math.Min(deltaT, 1)) * speed / 100f;
            if (lerp < 70)
            {
                _actPosition = _newPosition;
                return;
            }

            _actPosition = _actPosition.Extend(_newPosition, Math.Min(lerp / 4, 70));
        }

        private void MoveCursors(Vector3 pos)
        {
            if (MenuGUI.IsShopOpen)
            {
                _cursorMove.Position = Utils.GetCursorPos();
                _cursorAttack.Position = Utils.GetCursorPos();
            }
            else
            {
                _cursorMove.Position = Drawing.WorldToScreen(pos);
                _cursorAttack.Position = Drawing.WorldToScreen(pos);
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
                SetNewPos(args.End, true, IsThereUnit(args.End) || (args.Target != null));
            }
        }


        private void SetNewPos(Vector3 pos, bool isSpell = false, bool hasTarget = false)
        {
            var distance = (int) _player.Distance(pos);

            // HoldPosition
            if (distance < 30 && !isSpell)
            {
                return;
            }
            // Casted too far
            if (distance > 500 && isSpell && !hasTarget)
            {
                pos = _player.Position.Extend(pos, _rnd.Next(300, 700));
            }
            _lastClickTime = _newClickTime;
            _newPosition = pos;
            _newClickTime = Environment.TickCount;
        }
    }
}