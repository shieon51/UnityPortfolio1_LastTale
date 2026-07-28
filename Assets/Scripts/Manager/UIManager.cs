using System.Collections.Generic;

public class UIManager : Singleton<UIManager>
{
    public HUDStatusPanel hudStatus;
    public TimeCoinPanel timeCoin;
    public DialogueUIPanel dialogue;

    public void ShowDialogUI() => dialogue.Show();
    public void HideDialogUI() => dialogue.Hide();
    public void UpdateDialogueText(string text) => dialogue.UpdateText(text);
    public void ShowChoices(List<Ink.Runtime.Choice> choices) => dialogue.ShowChoices(choices);
    public void ClearChoices() => dialogue.ClearChoices();
}