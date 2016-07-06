using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Color = System.Drawing.Color;
using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;
using SharpDX.Direct3D9;
using SPrediction;
using UnderratedAIO.Helpers;
using UnderratedAIO.Helpers.SkillShot;
using Environment = UnderratedAIO.Helpers.Environment;
using Orbwalking = UnderratedAIO.Helpers.Orbwalking;
using Prediction = LeagueSharp.Common.Prediction;

namespace UnderratedAIO.Champions
{
    internal class Hecarim
    {
        public static Menu config;
        public static Orbwalking.Orbwalker orbwalker;
        public static AutoLeveler autoLeveler;
        public static Spell Q, W, E, R;
        public static readonly Obj_AI_Hero player = ObjectManager.Player;

        public Hecarim()
        {
            InitHecarim();
            InitMenu();
            Game.OnUpdate += Game_OnUpdate;
            Drawing.OnDraw += Drawing_OnDraw;
            Jungle.setSmiteSlot();
            Interrupter2.OnInterruptableTarget += Interrupter2_OnInterruptableTarget;
            HpBarDamageIndicator.DamageToUnit = ComboDamage;
        }

        private void Interrupter2_OnInterruptableTarget(Obj_AI_Hero sender,
            Interrupter2.InterruptableTargetEventArgs args)
        {
            if (sender == null)
            {
                return;
            }
            if (config.Item("AutoRinterrupt", true).GetValue<bool>() && R.CanCast(sender))
            {
                R.Cast(sender);
            }
        }

        private void Drawing_OnDraw(EventArgs args)
        {
            DrawHelper.DrawCircle(config.Item("drawqq", true).GetValue<Circle>(), Q.Range);
            DrawHelper.DrawCircle(config.Item("drawww", true).GetValue<Circle>(), W.Range);
            DrawHelper.DrawCircle(
                config.Item("drawee", true).GetValue<Circle>(), config.Item("useeRange", true).GetValue<Slider>().Value);
            DrawHelper.DrawCircle(config.Item("drawrr", true).GetValue<Circle>(), R.Range);
            HpBarDamageIndicator.Enabled = config.Item("drawcombo", true).GetValue<bool>();
            Helpers.Jungle.ShowSmiteStatus(
                config.Item("useSmite").GetValue<KeyBind>().Active, config.Item("smiteStatus").GetValue<bool>());
        }

        private void Game_OnUpdate(EventArgs args)
        {
            Jungle.CastSmite(config.Item("useSmite").GetValue<KeyBind>().Active);
            orbwalker.SetOrbwalkingPoint(Vector3.Zero);
            switch (orbwalker.ActiveMode)
            {
                case Orbwalking.OrbwalkingMode.Combo:
                    Combo();
                    break;
                case Orbwalking.OrbwalkingMode.Mixed:
                    Harass();
                    break;
                case Orbwalking.OrbwalkingMode.LaneClear:
                    Clear();
                    break;
                case Orbwalking.OrbwalkingMode.LastHit:
                    break;
                default:
                    break;
            }
            if (W.IsReady())
            {
                var dTaken =
                    HeroManager.Enemies.Where(a => a.Distance(player) < W.Range)
                        .Sum(a => Program.IncDamages.GetEnemyData(a.NetworkId).DamageTaken);
                if (dTaken * 0.2f > config.Item("AutoW", true).GetValue<Slider>().Value)
                {
                    W.Cast();
                }
            }
        }

        private void Harass()
        {
            Obj_AI_Hero target = TargetSelector.GetTarget(1000, TargetSelector.DamageType.Magical, true);
            float perc = config.Item("minmanaH", true).GetValue<Slider>().Value / 100f;
            if (player.Mana < player.MaxMana * perc || target == null)
            {
                return;
            }
            if (config.Item("useqH", true).GetValue<bool>() && Q.CanCast(target))
            {
                Q.Cast(config.Item("packets").GetValue<bool>());
            }
        }

        private void Clear()
        {
            float perc = config.Item("minmana", true).GetValue<Slider>().Value / 100f;
            if (player.Mana < player.MaxMana * perc)
            {
                return;
            }
            var jungleMobQ = Jungle.GetNearest(player.Position, Q.Range);
            var Qminis = MinionManager.GetMinions(Q.Range, MinionTypes.All, MinionTeam.NotAlly);
            if (config.Item("useqLC", true).GetValue<bool>() && Q.IsReady() &&
                (Qminis.Count >= config.Item("qMinHit", true).GetValue<Slider>().Value || jungleMobQ != null ||
                 (Qminis.Count(m => m.Health < Q.GetDamage(m)) > 0 && !Orbwalking.CanAttack())))
            {
                Q.Cast(config.Item("packets").GetValue<bool>());
            }
            if (config.Item("usewLC", true).GetValue<bool>() && W.IsReady() &&
                (MinionManager.GetMinions(W.Range, MinionTypes.All, MinionTeam.NotAlly).Count >=
                 config.Item("wMinHit", true).GetValue<Slider>().Value || jungleMobQ != null) &&
                Program.IncDamages.GetAllyData(player.NetworkId).DamageTaken > 50 && player.HealthPercent < 98)
            {
                W.Cast(config.Item("packets").GetValue<bool>());
            }
        }

        private void Combo()
        {
            Obj_AI_Hero target = TargetSelector.GetTarget(1000, TargetSelector.DamageType.Physical, true);
            if (target == null)
            {
                return;
            }
            if (config.Item("useq", true).GetValue<bool>() && Q.CanCast(target))
            {
                Q.Cast(config.Item("packets").GetValue<bool>());
            }
            if (config.Item("usew", true).GetValue<bool>() && W.IsInRange(target) &&
                Program.IncDamages.GetAllyData(player.NetworkId).DamageTaken > 50 && player.HealthPercent < 98)
            {
                W.Cast(config.Item("packets").GetValue<bool>());
            }
            if (config.Item("usee", true).GetValue<bool>() &&
                player.Distance(target) > Orbwalking.GetRealAutoAttackRange(target) + 50 &&
                player.Distance(target) < config.Item("useeRange", true).GetValue<Slider>().Value && E.IsReady())
            {
                E.Cast(config.Item("packets").GetValue<bool>());
            }
            if (config.Item("user", true).GetValue<bool>() && R.IsReady() && R.CanCast(target))
            {
                if (config.Item("useRbeforeCC", true).GetValue<bool>() &&
                    Program.IncDamages.GetAllyData(player.NetworkId).AnyCC)
                {
                    R.CastIfHitchanceEquals(target, HitChance.High, config.Item("packets").GetValue<bool>());
                }
                R.CastIfWillHit(target, config.Item("useRMinHit", true).GetValue<Slider>().Value);
            }
            if (config.Item("useItems").GetValue<bool>())
            {
                ItemHandler.UseItems(target, config, ComboDamage(target));
            }
            var ignitedmg = (float) player.GetSummonerSpellDamage(target, Damage.SummonerSpell.Ignite);
            bool hasIgnite = player.Spellbook.CanUseSpell(player.GetSpellSlot("SummonerDot")) == SpellState.Ready;
            if (config.Item("useIgnite", true).GetValue<bool>() &&
                ignitedmg > HealthPrediction.GetHealthPrediction(target, 1000) && hasIgnite &&
                !CombatHelper.CheckCriticalBuffs(target) &&
                ((player.HealthPercent < 35) ||
                 (target.Distance(player) > Orbwalking.GetRealAutoAttackRange(target) + 25)))
            {
                player.Spellbook.CastSpell(player.GetSpellSlot("SummonerDot"), target);
            }
        }


        private void InitMenu()
        {
            config = new Menu("Hecarim ", "Hecarim", true);
            // Target Selector
            Menu menuTS = new Menu("Selector", "tselect");
            TargetSelector.AddToMenu(menuTS);
            config.AddSubMenu(menuTS);
            // Orbwalker
            Menu menuOrb = new Menu("Orbwalker", "orbwalker");
            orbwalker = new Orbwalking.Orbwalker(menuOrb);
            config.AddSubMenu(menuOrb);
            // Draw settings
            Menu menuD = new Menu("Drawings ", "dsettings");
            menuD.AddItem(new MenuItem("drawqq", "Draw Q range", true))
                .SetValue(new Circle(false, Color.FromArgb(180, 87, 244, 255)));
            menuD.AddItem(new MenuItem("drawww", "Draw W range", true))
                .SetValue(new Circle(false, Color.FromArgb(180, 87, 244, 255)));
            menuD.AddItem(new MenuItem("drawee", "Draw E range", true))
                .SetValue(new Circle(false, Color.FromArgb(180, 87, 244, 255)));
            menuD.AddItem(new MenuItem("drawrr", "Draw R range", true))
                .SetValue(new Circle(false, Color.FromArgb(180, 87, 244, 255)));
            menuD.AddItem(new MenuItem("drawcombo", "Draw combo damage", true)).SetValue(true);
            config.AddSubMenu(menuD);
            // Combo Settings 
            Menu menuC = new Menu("Combo ", "csettings");
            menuC.AddItem(new MenuItem("useq", "Use Q", true)).SetValue(true);
            menuC.AddItem(new MenuItem("usew", "Use W", true)).SetValue(true);
            menuC.AddItem(new MenuItem("usee", "Use E", true)).SetValue(true);
            menuC.AddItem(new MenuItem("useeRange", "   Max range", true)).SetValue(new Slider(700, 350, 1000));
            menuC.AddItem(new MenuItem("user", "Use R", true)).SetValue(true);
            menuC.AddItem(new MenuItem("useRMinHit", "   Min hit", true)).SetValue(new Slider(3, 1, 6));
            menuC.AddItem(new MenuItem("useRbeforeCC", "   Before CC", true)).SetValue(true);
            menuC.AddItem(new MenuItem("useIgnite", "Use Ignite", true)).SetValue(true);
            menuC = ItemHandler.addItemOptons(menuC);
            config.AddSubMenu(menuC);
            // Harass Settings
            Menu menuH = new Menu("Harass ", "Hsettings");
            menuH.AddItem(new MenuItem("useqH", "Use Q", true)).SetValue(false);
            menuH.AddItem(new MenuItem("minmanaH", "Keep X% mana", true)).SetValue(new Slider(1, 1, 100));
            config.AddSubMenu(menuH);
            // LaneClear Settings
            Menu menuLC = new Menu("LaneClear ", "Lcsettings");
            menuLC.AddItem(new MenuItem("useqLC", "Use Q", true)).SetValue(true);
            menuLC.AddItem(new MenuItem("qMinHit", "   Min hit", true)).SetValue(new Slider(3, 1, 6));
            menuLC.AddItem(new MenuItem("usewLC", "Use W", true)).SetValue(true);
            menuLC.AddItem(new MenuItem("wMinHit", "   Min hit", true)).SetValue(new Slider(3, 1, 6));
            menuLC.AddItem(new MenuItem("minmana", "Keep X% mana", true)).SetValue(new Slider(20, 1, 100));
            config.AddSubMenu(menuLC);

            Menu menuM = new Menu("Misc ", "Msettings");
            menuM.AddSubMenu(Program.SPredictionMenu);
            menuM = Jungle.addJungleOptions(menuM);
            menuM.AddItem(new MenuItem("AutoRinterrupt", "Use R to interrupt", true)).SetValue(true);
            menuM.AddItem(new MenuItem("AutoW", "Use W to heal min", true)).SetValue(new Slider(100, 50, 500));
            Menu autolvlM = new Menu("AutoLevel", "AutoLevel");
            autoLeveler = new AutoLeveler(autolvlM);
            menuM.AddSubMenu(autolvlM);
            config.AddSubMenu(menuM);

            config.AddItem(new MenuItem("packets", "Use Packets")).SetValue(false);
            config.AddItem(new MenuItem("UnderratedAIO", "by Soresu v" + Program.version.ToString().Replace(",", ".")));
            config.AddToMainMenu();
        }

        private static float ComboDamage(Obj_AI_Hero hero)
        {
            double damage = 0;
            if (Q.IsReady())
            {
                damage += Damage.GetSpellDamage(player, hero, SpellSlot.Q);
            }
            if (W.IsReady())
            {
                damage += Damage.GetSpellDamage(player, hero, SpellSlot.W);
            }
            if (E.IsReady())
            {
                damage += Damage.GetSpellDamage(player, hero, SpellSlot.E);
            }
            if (R.IsReady())
            {
                damage += Damage.GetSpellDamage(player, hero, SpellSlot.R);
            }
            damage += ItemHandler.GetItemsDamage(hero);
            var ignitedmg = player.GetSummonerSpellDamage(hero, Damage.SummonerSpell.Ignite);
            if (player.Spellbook.CanUseSpell(player.GetSpellSlot("summonerdot")) == SpellState.Ready &&
                hero.Health < damage + ignitedmg)
            {
                damage += ignitedmg;
            }
            return (float) damage;
        }

        private void InitHecarim()
        {
            Q = new Spell(SpellSlot.Q, 350);
            W = new Spell(SpellSlot.W, 525);
            E = new Spell(SpellSlot.E);
            R = new Spell(SpellSlot.R, 1000);
            R.SetSkillshot(0.5f, 300, 1200, true, SkillshotType.SkillshotCircle);
        }
    }
}