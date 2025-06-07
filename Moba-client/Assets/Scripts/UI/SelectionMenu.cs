using SpacetimeDB.Types;
using System;
using TMPro;
using UnityEngine;

public class SelectionMenu : MonoBehaviour
{
    public static SelectionMenu instance;

    public TextMeshProUGUI attackText;
    public TextMeshProUGUI abilityPowerText;
    
    public TextMeshProUGUI armorText;
    public TextMeshProUGUI magicResistText;
    
    public TextMeshProUGUI attackSpeedText;
    public TextMeshProUGUI abilityHasteText;
    
    public TextMeshProUGUI critChanceText;
    public TextMeshProUGUI moveSpeedText;

    public ActorController target;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(this);
            Debug.LogError("too many selection menus in scene");
        }
    }

    public void Initialize(ActorController actorController)
    {
        target = actorController;

        attackText.text = actorController.GetAttackDamage().ToString();
        abilityPowerText.text = actorController.GetAbilityPower().ToString();

        armorText.text = actorController.GetArmor().ToString();
        magicResistText.text = actorController.GetMagicResist().ToString();
        
        attackSpeedText.text = actorController.GetAttackSpeed().ToString();
        abilityHasteText.text = actorController.GetAbilityHaste().ToString();

        critChanceText.text = actorController.GetCritChance().ToString();
        moveSpeedText.text = actorController.GetMoveSpeed().ToString();
    }
}
