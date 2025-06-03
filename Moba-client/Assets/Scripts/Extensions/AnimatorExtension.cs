using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class AnimatorExtension
{

    //usage --- myAnimator.SetTriggerOneFrame("Trigger", this)
    public static void SetTriggerOneFrame(this Animator animator, MonoBehaviour coroutineRunner, string trigger)
    {
        coroutineRunner.StartCoroutine(TriggerOneFrame(animator, trigger));
    }

    static IEnumerator TriggerOneFrame(Animator animator, string trigger)
    {
        animator.SetTrigger(trigger);
        yield return null;
        if (animator != null)
        {
            animator.ResetTrigger(trigger);
        }
    }
    public static void SetTriggerOneFixedFrame(this Animator animator, MonoBehaviour coroutineRunner, string trigger)
    {
        coroutineRunner.StartCoroutine(TriggerOneFixedFrame(animator, trigger));
    }

    static IEnumerator TriggerOneFixedFrame(Animator animator, string trigger)
    {
        animator.SetTrigger(trigger);
        yield return new WaitForFixedUpdate();
        if (animator != null)
        {
            animator.ResetTrigger(trigger);
        }
    }

    public static void SetTriggerXFrames(this Animator animator, int frames, MonoBehaviour coroutineRunner, string trigger)
    {
        coroutineRunner.StartCoroutine(TriggerXFrames(frames, animator, trigger));
    }

    static IEnumerator TriggerXFrames(int frames, Animator animator, string trigger)
    {
        animator.SetTrigger(trigger);

        for (int i = 0; i < frames; i++)
        {
            yield return null;
        }
        if (animator != null)
        {
            animator.ResetTrigger(trigger);
        }
    }
    public static void SetTriggerXFixedFrames(this Animator animator, int frames, MonoBehaviour coroutineRunner, string trigger)
    {
        coroutineRunner.StartCoroutine(TriggerXFixedFrames(frames, animator, trigger));
    }

    static IEnumerator TriggerXFixedFrames(int frames, Animator animator, string trigger)
    {
        animator.SetTrigger(trigger);

        for (int i = 0; i < frames; i++)
        {
            yield return new WaitForFixedUpdate();
        }
        if (animator != null)
        {
            animator.ResetTrigger(trigger);
        }
    }
}