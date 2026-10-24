// Attach to any object in scene for debug testing
using UnityEngine;
using Aeternal.Cards;
using Aeternal.Resources;
using Aeternal.Decks;

public class DebugCardPlay : MonoBehaviour
{
    public CardData card;
    public CardResolver resolver;
    public ResourceComponent resources;
    public DeckManager deck;
    public GameObject player;
    public GameObject enemy;

    [ContextMenu("Play Card")]
    public async void Play()
    {
        var ctx = new CardContext
        {
            caster = player,
            explicitTarget = enemy
        };
        await resolver.ResolvePlayAsync(card, ctx, resources, deck);
    }
}
