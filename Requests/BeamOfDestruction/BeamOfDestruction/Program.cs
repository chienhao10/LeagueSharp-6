using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using LeagueSharp;
using LeagueSharp.Common;
using Color = System.Drawing.Color;

namespace BeamOfDestruction
{
    internal class Program
    {
        private static Obj_AI_Hero _player;
        private static Menu _menu;
        private static Spell _beamOfDestruction;

        private static void Main(string[] args)
        {
            CustomEvents.Game.OnGameLoad += Game_OnGameLoad;
        }

        private static void Game_OnGameLoad(EventArgs args)
        {
            _player = ObjectManager.Player;
            CreateMenu();
            _beamOfDestruction = new Spell(SpellSlot.Unknown, 3100);
            _beamOfDestruction.SetSkillshot(0.65f, 150f, float.MaxValue, false, SkillshotType.SkillshotLine);
            Game.OnUpdate += Game_OnUpdate;
        }

        private static void Game_OnUpdate(EventArgs args)
        {
            if (!_menu.Item("manualUse").GetValue<KeyBind>().Active && !_menu.Item("autoUse").GetValue<bool>())
            {
                return;
            }

            if (_player.GetSpell(SpellSlot.Summoner1).Name == "SiegeLaserAffix")
            {
                _beamOfDestruction.Slot = SpellSlot.Summoner1;
            }
            else if (_player.GetSpell(SpellSlot.Summoner2).Name == "SiegeLaserAffix")
            {
                _beamOfDestruction.Slot = SpellSlot.Summoner1;
            }

            if (_player.HasBuff("SiegeLaserAffix"))
            {
                var turret =
                    ObjectManager.Get<Obj_AI_Turret>()
                        .Where(
                            t =>
                                t.HasBuff("siegelaseraffixactive") &&
                                t.GetBuff("siegelaseraffixactive").Caster.NetworkId == _player.NetworkId)
                        .OrderBy(t => t.Distance(_player))
                        .FirstOrDefault();
                if (turret != null)
                {
                    var target = TargetSelector.GetTarget(
                        3100, TargetSelector.DamageType.Magical, false,
                        HeroManager.Enemies.Where(e => e.Distance(turret) > _beamOfDestruction.Range));
                    if (target != null && _beamOfDestruction.IsReady())
                    {
                        if (_beamOfDestruction.GetPrediction(target).Hitchance >= HitChance.High)
                        {
                            _beamOfDestruction.Cast(target);
                        }
                    }
                }
            }
        }

        private static void CreateMenu()
        {
            _menu = new Menu("Beam of Destruction ", "BeamOfDestruction", true);

            _menu.AddItem(new MenuItem("autoUse", "Auto Cast BoD")).SetValue(true);
            _menu.AddItem(new MenuItem("manualUse", "Manual Cast"))
                .SetValue(new KeyBind("T".ToCharArray()[0], KeyBindType.Press))
                .SetFontStyle(System.Drawing.FontStyle.Bold, SharpDX.Color.Orange);

            _menu.AddItem(
                new MenuItem(
                    "UnderratedAIO",
                    "by Soresu v" + Assembly.GetExecutingAssembly().GetName().Version.ToString().Replace(",", ".")));
            _menu.AddToMainMenu();
        }
    }
}