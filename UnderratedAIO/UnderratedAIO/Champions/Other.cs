using System;
using System.Collections.Generic;
using System.Linq;
using Color = System.Drawing.Color;
using LeagueSharp;
using LeagueSharp.Common;
using UnderratedAIO.Helpers;
using UnderratedAIO.Helpers.SkillShot;
using Orbwalking = UnderratedAIO.Helpers.Orbwalking;

namespace UnderratedAIO.Champions
{
    internal class Other
    {
        public static Menu config;
        public static Orbwalking.Orbwalker orbwalker;
        public static AutoLeveler autoLeveler;
        public static readonly Obj_AI_Hero player = ObjectManager.Player;

        public Other()
        {
            InitMenu();
            Game.PrintChat("<font color='#9933FF'>Soresu </font><font color='#FFFFFF'>- Underrated AIO Common</font>");
            Helpers.Jungle.setSmiteSlot();
            Game.OnUpdate += Game_OnGameUpdate;
            Drawing.OnDraw += Game_OnDraw;
            Console.WriteLine(ObjectManager.Player.ChampionName);
        }


        private void Game_OnGameUpdate(EventArgs args)
        {
            if (config.Item("Enabledorb", true).GetValue<bool>())
            {
                orbwalker.Enabled = true;
            }
            else
            {
                orbwalker.Enabled = false;
            }
            if (config.Item("Enabledcomm").GetValue<bool>())
            {
                autoLeveler.enabled = true;
                switch (orbwalker.ActiveMode)
                {
                    case Orbwalking.OrbwalkingMode.Combo:
                        Combo();
                        break;
                    case Orbwalking.OrbwalkingMode.Mixed:
                        break;
                    case Orbwalking.OrbwalkingMode.LaneClear:
                        break;
                    case Orbwalking.OrbwalkingMode.LastHit:
                        break;
                    default:
                        break;
                }
                Jungle.CastSmite(config.Item("useSmite").GetValue<KeyBind>().Active);
            }
            else
            {
                autoLeveler.enabled = false;
            }
        }

        private void Combo()
        {
            Obj_AI_Hero target = TargetSelector.GetTarget(900, TargetSelector.DamageType.Physical);
            if (target == null)
            {
                return;
            }
            if (config.Item("useItems").GetValue<bool>())
            {
                ItemHandler.UseItems(target, config);
            }
            bool hasIgnite = player.Spellbook.CanUseSpell(player.GetSpellSlot("SummonerDot")) == SpellState.Ready;
            var ignitedmg = (float) player.GetSummonerSpellDamage(target, Damage.SummonerSpell.Ignite);
            if (config.Item("useIgnite").GetValue<bool>() && ignitedmg > target.Health && hasIgnite &&
                player.Distance(target) > Orbwalking.GetRealAutoAttackRange(target))
            {
                player.Spellbook.CastSpell(player.GetSpellSlot("SummonerDot"), target);
            }
        }

        private void Game_OnDraw(EventArgs args)
        {
            if (config.Item("Enabledcomm").GetValue<bool>())
            {
                Helpers.Jungle.ShowSmiteStatus(
                    config.Item("useSmite").GetValue<KeyBind>().Active, config.Item("smiteStatus").GetValue<bool>());
            }
        }

        private void InitMenu()
        {
            config = new Menu("UnderratedAIO", "UnderratedAIO", true);
            // Target Selector
            Menu menuTS = new Menu("Selector", "tselect");
            TargetSelector.AddToMenu(menuTS);
            config.AddSubMenu(menuTS);
            // Orbwalker
            Menu menuOrb = new Menu("Orbwalker", "orbwalker");
            orbwalker = new Orbwalking.Orbwalker(menuOrb);
            config.AddSubMenu(menuOrb);
            Menu menuC = new Menu("Combo ", "csettings");
            menuC = ItemHandler.addItemOptons(menuC);
            menuC.AddItem(new MenuItem("useIgnite", "Ignite")).SetValue(true);
            config.AddSubMenu(menuC);
            Menu menuM = new Menu("Misc ", "Msettings");
            menuM = Jungle.addJungleOptions(menuM);
            Menu autolvlM = new Menu("AutoLevel", "AutoLevel");
            autoLeveler = new AutoLeveler(autolvlM);
            menuM.AddSubMenu(autolvlM);
            config.AddSubMenu(menuM);
            config.AddItem(new MenuItem("Enabledcomm", "Enable Utilies")).SetValue(true);
            config.AddItem(new MenuItem("Enabledorb", "Enable OrbWalker", true)).SetValue(true);
            config.AddItem(new MenuItem("UnderratedAIO", "by Soresu v" + Program.version.ToString().Replace(",", ".")));
            config.AddToMainMenu();
        }
    }
}