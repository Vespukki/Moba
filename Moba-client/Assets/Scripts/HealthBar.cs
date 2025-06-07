using SpacetimeDB.Types;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;

public class HealthBar : MonoBehaviour
{
    public GameObject bigBarPrefab;
    public GameObject smallBarPrefab;

    public Transform parentTransform;

    public float fullLength = 1;
    public int maxTicks = 24;

    List<GameObject> oldBigBars = new();
    List<GameObject> oldSmallBars = new();

    float lastMaxHealth;
    float lastCurrentHealth;

    public GameObject greenPart;

    private void Awake()
    {
        float width = GetComponent<RectTransform>().rect.width;
        float height = GetComponent<RectTransform>().rect.height;


        greenPart.transform.position = new(transform.position.x, transform.position.y, transform.position.z);

        fullLength = (1 - GetComponent<RectTransform>().pivot.x) * width;
    }

    private void Update()   
    {
        
    }

    public void ChangeMaxHealth(int newMaxHealth)
    {
        Debug.Log("Changing max health");
        foreach (var bar in oldBigBars)
        {
            Destroy(bar);
        }
        oldBigBars.Clear();
        foreach (var bar in oldSmallBars)
        {
            Destroy(bar);
        }
        oldSmallBars.Clear();

        int maxHealthLeft = newMaxHealth;

        int bigBarCount = 0;

        while (maxHealthLeft > 1000)
        {
            maxHealthLeft -= 1000;
            bigBarCount++;
        }

        float lenPerHp = fullLength / newMaxHealth;

        int ticksPerBar = 9;

        int maxTotalTicks = maxTicks - bigBarCount;

        if (bigBarCount != 0)
        {
            ticksPerBar = ((maxTotalTicks - (maxTotalTicks % bigBarCount)) / bigBarCount);
        }
        if (ticksPerBar > 9) ticksPerBar = 9;

        float tickDist = 1000f / ((float)ticksPerBar + 1f);

        for (int i = 0; i < bigBarCount + 1; i++)
        {
            float bigOffset = (i * 1000 * lenPerHp);
            for (int j = 1; j < ticksPerBar + 1; j++)
            {
                float smallOffset = (j * tickDist * lenPerHp);

                if (smallOffset + bigOffset > fullLength) continue;

                var smallBar = Instantiate(smallBarPrefab, transform);
                oldSmallBars.Add(smallBar);
                smallBar.transform.position = new(transform.position.x + bigOffset + smallOffset, transform.position.y, transform.position.z);
            }

            if (i == 0) continue;
            var bigBar = Instantiate(bigBarPrefab, transform);
            oldBigBars.Add(bigBar);
            bigBar.transform.position = new(transform.position.x + bigOffset, transform.position.y, transform.position.z);
        }

        
    }

    public void ChangeCurrentHealth(float newCurrentHealth, float maxHealth)
    {
        float ratio = (float)newCurrentHealth / (float)maxHealth;
        ratio = Mathf.Clamp(ratio, 0, 1);
        greenPart.transform.localScale = new Vector3(ratio, greenPart.transform.localScale.y, greenPart.transform.localScale.z);
    }

    public void UpdateHealth(Actor actor, ActorBaseStats stats)
    {
        int maxHealth = (int)stats.MaxHealth;
        int currentHealth = (int)actor.CurrentHealth;

        if (maxHealth != lastMaxHealth)
        {
            lastMaxHealth = maxHealth;
            ChangeMaxHealth(maxHealth);

        }

        if (currentHealth != lastCurrentHealth)
        {
            lastCurrentHealth = currentHealth;
            ChangeCurrentHealth(currentHealth, maxHealth);
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.blue;

        Gizmos.DrawSphere(transform.position + new Vector3(fullLength, 0, 0), .02f);
    }
}
