using UnityEngine;

public sealed partial class LateGamePresenter
{
    // Presentation only. Both hits have already happened; no plant reference or gameplay callback.
    private void Overflow(RewardCue cue)
    {
        Vector3 delta = cue.To - cue.From;
        if (delta.sqrMagnitude < .0025f) return;
        bool continuation = cue.TransferStep > 0;
        float size = continuation ? .65f : 1f;
        Vector3 lift = Vector3.up * .65f;
        var variant = LateGameVariant.Overflow;
        Item piece = Place(flashes, LateGameSlot.ArcBolt, Hot, cue.From + lift,
            new Vector3(size, size, continuation ? .5f : 1f), 0f, true, variant);
        if (piece != null)
        {
            piece.root.transform.rotation = Quaternion.LookRotation(delta.normalized);
            piece.overflowTravel = true;
            piece.travelFrom = cue.From + lift;
            piece.travelTo = cue.To + lift;
            piece.life = (Art != null ? Art.Lifetime(LateGameSlot.ArcBolt, variant) : .14f) * (continuation ? .75f : 1f);
        }
        // Immediate actual-hit feedback; travel never schedules damage or another impact.
        Item contact = Place(flashes, LateGameSlot.AntennaContact, Hot, cue.To + lift,
            Vector3.one * (continuation ? .38f : .55f), 0f, true, variant);
        if (contact != null) contact.life = Art != null ? Art.Lifetime(LateGameSlot.AntennaContact, variant) : .12f;
        if (!continuation) Play(LateGameSound.Overflow, variant);
    }
}
