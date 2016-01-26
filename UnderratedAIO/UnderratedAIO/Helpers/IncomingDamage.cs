using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LeagueSharp;
using LeagueSharp.Common;
using Orbwalking = UnderratedAIO.Helpers.Orbwalking;

namespace UnderratedAIO.Helpers
{
    internal class IncomingDamage
    {
        public List<IncData> IncomingDamagesAlly = new List<IncData>();
        public List<IncData> IncomingDamagesEnemy = new List<IncData>();
        public bool enabled;

        public IncData GetAllyData(int networkId)
        {
            return IncomingDamagesAlly.FirstOrDefault(i => i.Hero.NetworkId == networkId);
        }

        public IncData GetEnemyData(int networkId)
        {
            return IncomingDamagesEnemy.FirstOrDefault(i => i.Hero.NetworkId == networkId);
        }

        public void Debug()
        {
            var data = IncomingDamagesAlly.Concat(IncomingDamagesEnemy);
            foreach (var d in data)
            {
                Console.WriteLine(d.Hero.Name);
                Console.WriteLine("\t DamageCount: " + d.DamageCount);
                Console.WriteLine("\t DamageCount: " + d.AADamageCount);
                Console.WriteLine("\t DamageTaken: " + d.DamageTaken);
                Console.WriteLine("\t DamageTaken: " + d.AADamageTaken);
                Console.WriteLine("\t TargetedCC: " + d.TargetedCC);
            }
        }

        public IncomingDamage()
        {
            Obj_AI_Base.OnProcessSpellCast += Game_ProcessSpell;
            Game.OnUpdate += Game_OnGameUpdate;
            foreach (var ally in ObjectManager.Get<Obj_AI_Hero>().Where(h => h.IsAlly))
            {
                IncomingDamagesAlly.Add(new IncData(ally));
            }
            foreach (var Enemy in ObjectManager.Get<Obj_AI_Hero>().Where(h => h.IsEnemy))
            {
                IncomingDamagesEnemy.Add(new IncData(Enemy));
            }
            enabled = true;
        }

        private void Game_OnGameUpdate(EventArgs args)
        {
            if (ObjectManager.Player.IsDead)
            {
                resetAllData();
            }
            else
            {
                resetData();
            }
        }

        private void resetData()
        {
            foreach (var incDamage in
                IncomingDamagesAlly.Concat(IncomingDamagesEnemy))
            {
                for (int index = 0; index < incDamage.Damages.Count; index++)
                {
                    var d = incDamage.Damages[index];
                    if (Game.Time - d.Time > d.delete)
                    {
                        incDamage.Damages.RemoveAt(index);
                        if (incDamage.DamageCount > 0)
                        {
                            incDamage.DamageCount--;
                        }
                    }
                }
            }
        }

        private void resetAllData()
        {
            foreach (var incDamage in
                IncomingDamagesAlly.Concat(IncomingDamagesEnemy))
            {
                incDamage.Damages.Clear();
                incDamage.DamageCount = 0;
            }
        }

        private void Game_ProcessSpell(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            if (enabled)
            {
                Obj_AI_Hero target = args.Target as Obj_AI_Hero;
                if (target != null && target.Team != sender.Team)
                {
                    if (sender.IsValid && !sender.IsDead)
                    {
                        var data =
                            IncomingDamagesAlly.Concat(IncomingDamagesEnemy)
                                .FirstOrDefault(i => i.Hero.NetworkId == target.NetworkId);
                        if (data != null)
                        {
                            var missileSpeed = sender.Distance(target) / args.SData.MissileSpeed;
                            missileSpeed = missileSpeed > 1f ? 0.8f : missileSpeed;
                            if (Orbwalking.IsAutoAttack(args.SData.Name))
                            {
                                var dmg = (float) sender.GetAutoAttackDamage(target, true);
                                data.Damages.Add(new Dmg(dmg, missileSpeed, true));
                                data.DamageCount++;
                            }
                            else
                            {
                                var hero = sender as Obj_AI_Hero;
                                if (hero != null)
                                {
                                    data.Damages.Add(
                                        new Dmg(
                                            (float) Damage.GetSpellDamage(hero, (Obj_AI_Base) args.Target, args.Slot),
                                            missileSpeed, CombatHelper.isTargetedCC(args.SData.Name, true)));
                                    data.DamageCount++;
                                }
                            }
                        }
                    }
                    //Debug();
                }
            }
        }
    }

    internal class IncData
    {
        public List<Dmg> Damages = new List<Dmg>();
        public int DamageCount;
        public Obj_AI_Hero Hero;


        public float DamageTaken
        {
            get { return Damages.Sum(d => d.DamageTaken); }
        }

        public float HealthPrediction
        {
            get { return Hero.Health - DamageTaken; }
        }

        public bool TargetedCC
        {
            get { return Damages.Any(d => d.TargetedCC); }
        }

        public float AADamageTaken
        {
            get { return Damages.Where(d => d.isAA).Sum(d => d.DamageTaken); }
        }

        public float AADamageCount
        {
            get { return Damages.Count(d => d.isAA); }
        }

        public IncData(Obj_AI_Hero _hero)
        {
            this.Hero = _hero;
        }
    }

    internal class Dmg
    {
        public float DamageTaken;
        public float Time;
        public float delete;
        public bool isAA;
        public bool TargetedCC;

        public Dmg(float dmg, float delete, bool isAA = false, bool TargetedCC = false)
        {
            DamageTaken = dmg;
            Time = Game.Time;
            this.delete = delete;
            this.isAA = isAA;
            this.TargetedCC = TargetedCC;
        }
    }
}