using System.Collections.Generic;
using UnityEngine;

namespace MWL_Ports;

public class PortTrader : MonoBehaviour
{
    public float m_standRange = 15f;
    public float m_greetRange = 5f;
    public float m_byeRange = 5f;
    public float m_hideDialogDelay = 5f;
    public float m_randomTalkInterval = 30f;
    public float m_dialogHeight = 1.5f;
    public static readonly List<string> m_randomTalk = new();
    public static readonly List<string> m_randomGreets = new();
    public static readonly List<string> m_randomGoodbye = new();
    public EffectList m_randomTalkFX = new();
    public EffectList m_randomGreetFX = new();
    public EffectList m_randomGoodbyeFX = new();
    public bool m_didGreet;
    public bool m_didGoodbye;
    public Animator m_animator = null!;
    public LookAt m_lookAt = null!;
    private static readonly int stand = Animator.StringToHash("Stand");
    public void Start()
    {
        m_animator = GetComponentInChildren<Animator>();
        m_lookAt = GetComponentInChildren<LookAt>();
        InvokeRepeating(nameof(RandomTalk), m_randomTalkInterval, m_randomTalkInterval);
    }

    public void Update()
    {
        Player? closestPlayer = Player.GetClosestPlayer(transform.position, Mathf.Max(m_byeRange + 3f, m_standRange));
        if (closestPlayer)
        {
            float distance = Vector3.Distance(closestPlayer.transform.position, transform.position);
            if (distance < (double)m_standRange)
            {
                m_animator.SetBool(stand, true);
                m_lookAt.SetLoockAtTarget(closestPlayer.GetHeadPoint());
            }

            if (!m_didGreet && distance < (double)m_greetRange)
            {
                m_didGreet = true;
                Say(m_randomGreets, "Greet");
                m_randomGreetFX.Create(transform.position, Quaternion.identity);
            }

            if (!m_didGreet || m_didGoodbye || distance <= (double)m_byeRange)
                return;
            m_didGoodbye = true;
            Say(m_randomGoodbye, "Greet");
            m_randomGoodbyeFX.Create(transform.position, Quaternion.identity);
        }
        else
        {
            m_animator.SetBool(stand, false);
            m_lookAt.ResetTarget();
        }
    }

    public void RandomTalk()
    {
        if (!m_animator.GetBool(stand) || StoreGui.IsVisible() || !Player.IsPlayerInRange(transform.position, m_greetRange)) return;
        Say(m_randomTalk, "Talk");
        m_randomTalkFX.Create(transform.position, Quaternion.identity);
    }
    
    public void Say(List<string> texts, string trigger)
    {
        Say(texts[Random.Range(0, texts.Count)], trigger);
    }

    public void Say(string text, string trigger)
    {
        Chat.instance.SetNpcText(gameObject, Vector3.up * m_dialogHeight, 20f, m_hideDialogDelay, "", text, false);
        if (trigger.Length <= 0) return;
        m_animator.SetTrigger(trigger);
    }
}