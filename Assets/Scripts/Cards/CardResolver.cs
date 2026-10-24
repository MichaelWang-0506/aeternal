using System.Collections.Generic;
using UnityEngine;
using Aeternal.Stats;
using Aeternal.Decks;
using Aeternal.Events;
using Aeternal.Player;
using Aeternal.UI;
using System.Threading.Tasks;

namespace Aeternal.Cards
{
    // Central executor for EffectPayloads. Keep this class focused on data-driven execution.
    [DefaultExecutionOrder(100)]
    public class CardResolver : MonoBehaviour
    {
        // Resolve a full card play synchronously (can extend to async for animations)
        public bool CanPlay(CardData card, Resources.ResourceComponent resource)
        {
            if (card == null || resource == null) return false;
            if (resource.currentAP < card.costAP) return false;
            return true;
        }

        public async Task<bool> ResolvePlayAsync(CardData card, CardContext ctx, Resources.ResourceComponent resource, DeckManager deckManager)
        {
            if (card == null) return false;

            // Check if caster can act (not affected by Blackout)
            if (ctx.caster != null)
            {
                var statusManager = ctx.caster.GetComponent<Aeternal.Status.StatusManager>();
                if (statusManager != null && !statusManager.CanAct())
                {
                    string msg = "Cannot play card: affected by Blackout";
                    EventBus.RaiseLog(msg);
                    ActionLog.Instance?.AddMessage(msg);
                    return false;
                }
            }

            if (!CanPlay(card, resource))
            {
                string msg = "Cannot play card: insufficient resources";
                EventBus.RaiseLog(msg);
                ActionLog.Instance?.AddMessage(msg);
                return false;
            }

            // consume costs
            resource.TryConsumeAP(card.costAP);

            // Build action description
            string actionDesc = $"Player used {card.id}";
            List<string> effects = new List<string>();

            // execute effects sequentially and track what happened
            foreach (var eff in card.effects)
            {
                string effectDesc = ResolveEffect(eff, ctx, deckManager);
                if (!string.IsNullOrEmpty(effectDesc))
                    effects.Add(effectDesc);
            }

            // Log the action with results
            if (effects.Count > 0)
            {
                actionDesc += $" - {string.Join(", ", effects)}";
            }
            ActionLog.Instance?.AddMessage(actionDesc);
            EventBus.RaiseLog($"[Resolver] Finished {card.id}");
            await Task.Yield();

            // Always discard (exhaust logic can be added via status effects if needed)
            deckManager.Discard(card);

            return true;
        }

        /// <summary>
        /// Resolves an effect and returns a description of what happened
        /// </summary>
        public string ResolveEffect(EffectPayload eff, CardContext ctx, DeckManager deckManager)
        {
            switch (eff.effectType)
            {
                case EffectType.Damage:
                    return ExecuteDamage(eff, ctx);
                case EffectType.Heal:
                    return ExecuteHeal(eff, ctx);
                case EffectType.AddShield:
                    return ExecuteAddShield(eff, ctx);
                case EffectType.ModifyAP:
                    return ExecuteModifyAP(eff, ctx);
                case EffectType.ModifyDF:
                    return ExecuteModifyDF(eff, ctx);
                case EffectType.DrawCards:
                    return ExecuteDrawCards(eff, ctx, deckManager);
                case EffectType.RecycleCards:
                    return ExecuteRecycleCards(eff, ctx, deckManager);
                case EffectType.Move:
                    return ExecuteMove(eff, ctx);
                case EffectType.ApplyStatus:
                    return ExecuteApplyStatus(eff, ctx);
                case EffectType.EraseAllEffects:
                    return ExecuteEraseAllEffects(eff, ctx);
                default:
                    EventBus.RaiseLog($"Effect type not implemented: {eff.effectType}");
                    return null;
            }
        }

        private string ExecuteDamage(EffectPayload eff, CardContext ctx)
        {
            if (ctx == null || ctx.caster == null)
            {
                EventBus.RaiseLog("[Resolver] Damage effect has no context or caster");
                return null;
            }

            // Determine target based on EffectTarget
            GameObject target = null;
            if (eff.target == EffectTarget.Enemy)
            {
                target = ctx.explicitTarget ?? ctx.enemy;
            }
            else if (eff.target == EffectTarget.Self)
            {
                target = ctx.caster;
            }

            if (target == null)
            {
                EventBus.RaiseLog("[Resolver] Damage effect has no valid target");
                return null;
            }

            // Safety check: prevent damaging the caster unless explicitly targeting self
            if (eff.target != EffectTarget.Self && ctx.caster != null && target == ctx.caster)
            {
                EventBus.RaiseLog($"[Resolver] Warning: Attempted to damage caster {ctx.caster.name}. Skipping to prevent self-damage bug.");
                return null;
            }

            var hc = target.GetComponent<HealthComponent>();
            if (hc == null)
            {
                EventBus.RaiseLog($"[Resolver] Target {target.name} has no HealthComponent");
                return null;
            }

            // Set source to caster to track who dealt the damage
            var info = new DamageInfo(eff.magnitude, eff.damageType, ctx.caster);
            
            int healthBefore = hc.getHealth;
            hc.TakeDamage(info);
            int healthAfter = hc.getHealth;
            int actualDamage = healthBefore - healthAfter;

            string targetName = target.name;
            if (target.GetComponent<Aeternal.Enemies.EnemyController>() != null)
            {
                targetName = "Enemy";
            }
            else if (target.GetComponent<Aeternal.Player.PlayerPositioningComponent>() != null)
            {
                targetName = "Player";
            }

            return $"Dealt {actualDamage} {eff.damageType} damage to {targetName}";
        }

        

        private string ExecuteHeal(EffectPayload eff, CardContext ctx)
        {
            if (ctx == null || ctx.caster == null) return null;

            // Determine target based on EffectTarget
            GameObject target = null;
            if (eff.target == EffectTarget.Enemy)
            {
                target = ctx.explicitTarget ?? ctx.enemy;
            }
            else if (eff.target == EffectTarget.Self)
            {
                target = ctx.caster;
            }

            if (target == null) return null;
            var hc = target.GetComponent<HealthComponent>();
            if (hc == null) return null;

            int healthBefore = hc.getHealth;
            hc.Heal(eff.magnitude);
            int healthAfter = hc.getHealth;
            int actualHeal = healthAfter - healthBefore;

            string targetName = target.name;
            if (target.GetComponent<Aeternal.Player.PlayerPositioningComponent>() != null)
                targetName = "Player";

            return actualHeal > 0 ? $"Healed {targetName} for {actualHeal}" : null;
        }

        private string ExecuteAddShield(EffectPayload eff, CardContext ctx)
        {
            if (ctx == null || ctx.caster == null) return null;

            // Determine target based on EffectTarget
            GameObject target = null;
            if (eff.target == EffectTarget.Enemy)
            {
                target = ctx.explicitTarget ?? ctx.enemy;
            }
            else if (eff.target == EffectTarget.Self)
            {
                target = ctx.caster;
            }

            if (target == null) return null;
            var hc = target.GetComponent<HealthComponent>();
            if (hc == null) return null;

            hc.AddShield(eff.magnitude, eff.durationRounds, null);

            string targetName = target.name;
            if (target.GetComponent<Aeternal.Player.PlayerPositioningComponent>() != null)
                targetName = "Player";

            return $"Added {eff.magnitude} shield to {targetName}";
        }

        private string ExecuteModifyAP(EffectPayload eff, CardContext ctx)
        {
            var resource = ctx.caster?.GetComponent<Resources.ResourceComponent>();
            if (resource == null) return null;

            int apBefore = resource.currentAP;
            resource.ModifyAP(eff.magnitude);
            int apAfter = resource.currentAP;
            int actualChange = apAfter - apBefore;

            if (actualChange == 0) return null;
            string sign = actualChange > 0 ? "+" : "";
            return $"AP {sign}{actualChange}";
        }

        private string ExecuteModifyDF(EffectPayload eff, CardContext ctx)
        {
            var resource = ctx.caster?.GetComponent<Resources.ResourceComponent>();
            if (resource == null) return null;

            int dfBefore = resource.currentDF;
            resource.ModifyDF(eff.magnitude);
            int dfAfter = resource.currentDF;
            int actualChange = dfAfter - dfBefore;

            if (actualChange == 0) return null;
            string sign = actualChange > 0 ? "+" : "";
            return $"DF {sign}{actualChange}";
        }

        private string ExecuteDrawCards(EffectPayload eff, CardContext ctx, DeckManager deckManager)
        {
            int drawn = 0;
            for (int i = 0; i < eff.magnitude; i++)
            {
                var c = deckManager.DrawOne();
                if (c != null)
                {
                    deckManager.hand.Add(c);
                    drawn++;
                }
            }

            if (drawn == 0) return null;
            return $"Drew {drawn} card{(drawn > 1 ? "s" : "")}";
        }

        private string ExecuteRecycleCards(EffectPayload eff, CardContext ctx, DeckManager deckManager)
        {
            if (deckManager == null) return null;

            int recycled = deckManager.RecycleCardsFromDiscard(eff.magnitude);

            if (recycled == 0) return null;
            return $"Recycled {recycled} card{(recycled > 1 ? "s" : "")} from discard pile";
        }

        private string ExecuteMove(EffectPayload eff, CardContext ctx)
        {
            if (ctx == null || ctx.caster == null) return null;

            var pc = ctx.caster.GetComponent<PlayerPositioningComponent>();
            if (pc == null) return null;

            // Move by magnitude squares (positive = clockwise, negative = counterclockwise)
            int newFacingIndex = pc.facingIndex;
            if (eff.magnitude > 0)
            {
                // Clockwise movement
                for (int i = 0; i < eff.magnitude; i++)
                {
                    newFacingIndex = (newFacingIndex + 1) % 4;
                }
            }
            else if (eff.magnitude < 0)
            {
                // Counterclockwise movement
                for (int i = 0; i < -eff.magnitude; i++)
                {
                    newFacingIndex = (newFacingIndex - 1 + 4) % 4;
                }
            }

            // Apply the movement using the proper method that triggers the event
            pc.SetFacingIndex(newFacingIndex);

            string[] directions = { "Up", "Right", "Down", "Left" };
            return $"Moved {directions[newFacingIndex]}";
        }

        private string ExecuteApplyStatus(EffectPayload eff, CardContext ctx)
        {
            if (ctx == null || ctx.caster == null || eff.statusEffectData == null) return null;

            // Determine target based on EffectTarget
            GameObject target = null;
            if (eff.target == EffectTarget.Enemy)
            {
                target = ctx.explicitTarget ?? ctx.enemy;
            }
            else if (eff.target == EffectTarget.Self)
            {
                target = ctx.caster;
            }

            if (target == null) return null;

            var sm = target.GetComponent<Aeternal.Status.StatusManager>();
            if (sm == null) return null;

            // Add status with the behavior data
            sm.AddStatus(eff.statusEffectData.statusId, eff.magnitude, eff.durationRounds, eff.statusEffectData);

            string targetName = target.name;
            if (target.GetComponent<Aeternal.Player.PlayerPositioningComponent>() != null)
                targetName = "Player";
            else if (target.GetComponent<Aeternal.Enemies.EnemyController>() != null)
                targetName = "Enemy";

            return $"Applied {eff.statusEffectData.statusId} to {targetName}";
        }

        private string ExecuteEraseAllEffects(EffectPayload eff, CardContext ctx)
        {
            if (ctx == null || ctx.caster == null) return null;

            // Determine target based on EffectTarget
            GameObject target = null;
            if (eff.target == EffectTarget.Enemy)
            {
                target = ctx.explicitTarget ?? ctx.enemy;
            }
            else if (eff.target == EffectTarget.Self)
            {
                target = ctx.caster;
            }

            if (target == null) return null;

            var sm = target.GetComponent<Aeternal.Status.StatusManager>();
            if (sm == null) return null;

            int count = sm.activeStatuses.Count;
            sm.RemoveAllStatuses();

            string targetName = target.name;
            if (target.GetComponent<Aeternal.Player.PlayerPositioningComponent>() != null)
                targetName = "Player";
            else if (target.GetComponent<Aeternal.Enemies.EnemyController>() != null)
                targetName = "Enemy";

            return $"Erased {count} status effect{(count != 1 ? "s" : "")} from {targetName}";
        }
    }
}

