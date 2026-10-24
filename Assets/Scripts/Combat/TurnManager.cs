using UnityEngine;
using System.Collections;
using Aeternal.Events;

namespace Aeternal.Combat
{
    public enum TurnState { None, PlayerPhase, EnemyPhase, EndRound }

    public class TurnManager : MonoBehaviour
    {
        public TurnState currentState = TurnState.None;
        public float phaseDelay = 0.5f;

        public delegate void TurnDelegate();
        public event TurnDelegate OnRoundStart;
        public event TurnDelegate OnPlayerPhaseStart;
        public event TurnDelegate OnEnemyPhaseStart;
        public event TurnDelegate OnRoundEnd;

        private bool running;

        public void StartCombat()
        {
            if (running) return;
            StartCoroutine(RunCombatLoop());
        }

        public IEnumerator RunCombatLoop()
        {
            running = true;
            while (running)
            {
                yield return StartCoroutine(RoundRoutine());
            }
        }

        private IEnumerator RoundRoutine()
        {
            OnRoundStart?.Invoke();
            yield return new WaitForSeconds(phaseDelay);

            currentState = TurnState.PlayerPhase;
            OnPlayerPhaseStart?.Invoke();
            yield return new WaitUntil(() => currentState != TurnState.PlayerPhase);

            currentState = TurnState.EnemyPhase;
            OnEnemyPhaseStart?.Invoke();
            yield return new WaitForSeconds(phaseDelay);

            currentState = TurnState.EndRound;
            OnRoundEnd?.Invoke();
            yield return new WaitForSeconds(phaseDelay);

            currentState = TurnState.None;
        }

        public void EndPlayerPhase()
        {
            if (currentState == TurnState.PlayerPhase)
                currentState = TurnState.EnemyPhase;
        }

        public void StopCombat()
        {
            running = false;
            // Immediately abort any round currently in flight so a dead actor's phase
            // (e.g. the enemy attacking after taking a lethal blow) does not still resolve.
            StopAllCoroutines();
            currentState = TurnState.None;
        }
    }
}
