﻿using avaness.PluginLoader.Data;
using avaness.PluginLoader.Stats;
using avaness.PluginLoader.Stats.Model;
using Sandbox.Graphics.GUI;
using System;
using System.Collections.Generic;
using System.Text;
using VRage.Game;
using VRage.Utils;
using VRageMath;

namespace avaness.PluginLoader.GUI
{
    public class PluginDetailMenu : PluginScreen
    {
        private HashSet<string> enabledPlugins;
        private PluginData plugin;
        private PluginInstance pluginInstance;
        private PluginStat stats;
        private MyGuiControlParent votingPanel;

        /// <summary>
        /// Called when a development folder plugin is removed
        /// </summary>
        public event Action<PluginData> OnPluginRemoved;
        
        public event Action OnRestartRequired;

        public PluginDetailMenu(PluginData plugin, HashSet<string> enabledPlugins) : base(size: new Vector2(0.5f, 0.8f))
        {
            this.plugin = plugin;
            this.enabledPlugins = enabledPlugins;
            if (Main.Instance.TryGetPluginInstance(plugin.Id, out PluginInstance instance))
                pluginInstance = instance;
            PluginStats stats = Main.Instance.Stats ?? new PluginStats();
            this.stats = stats.GetStatsForPlugin(plugin);
        }

        public override string GetFriendlyName()
        {
            return typeof(PluginDetailMenu).FullName;
        }

        public override void RecreateControls(bool constructor)
        {
            base.RecreateControls(constructor);

            // Top
            MyGuiControlLabel caption = AddCaption(plugin is ModPlugin ? "Mod Details" : "Plugin Details", captionScale: 1);
            AddBarBelow(caption);

            // Bottom
            Vector2 halfSize = m_size.Value / 2;
            MyGuiControlLabel lblSource = new MyGuiControlLabel(text: plugin.Source, position: new Vector2(GuiSpacing - halfSize.X, halfSize.Y - GuiSpacing), originAlign: MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_BOTTOM);
            Controls.Add(lblSource);

            Vector2 buttonPos = new Vector2(0, halfSize.Y - (lblSource.Size.Y + GuiSpacing));
            MyGuiControlButton btnInfo = new MyGuiControlButton(position: new Vector2(buttonPos.X - (GuiSpacing / 2), buttonPos.Y - GuiSpacing), text: new StringBuilder("More Info"), originAlign: MyGuiDrawAlignEnum.HORISONTAL_RIGHT_AND_VERTICAL_BOTTOM, onButtonClick: OnPluginOpenClick);
            Controls.Add(btnInfo);

            MyGuiControlButton btnSettings = new MyGuiControlButton(text: new StringBuilder("Settings"), onButtonClick: OnPluginSettingsClick);
            btnSettings.Enabled = pluginInstance != null && pluginInstance.HasConfigDialog;
            PositionToRight(btnInfo, btnSettings);
            Controls.Add(btnSettings);

            plugin.AddDetailControls(this, btnInfo, out MyGuiControlBase bottomControl);
            if (bottomControl == null)
                bottomControl = btnInfo;

            // Center
            MyLayoutTable layout = GetLayoutTableBetween(caption, bottomControl, verticalSpacing: GuiSpacing * 2);
            layout.SetColumnWidthsNormalized(0.5f, 0.5f);
            layout.SetRowHeightsNormalized(0.05f, 0.05f, 0.05f, 0.85f);

            layout.Add(new MyGuiControlLabel(text: plugin.FriendlyName, textScale: 0.9f), MyAlignH.Left, MyAlignV.Bottom, 0, 0);
            layout.Add(new MyGuiControlLabel(text: plugin.Author), MyAlignH.Left, MyAlignV.Top, 1, 0);

            MyGuiControlMultilineText descriptionText = new MyGuiControlMultilineText(textAlign: MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP, textBoxAlign: MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP)
            {
                VisualStyle = MyGuiControlMultilineStyleEnum.BackgroundBordered
            };
            layout.AddWithSize(descriptionText, MyAlignH.Center, MyAlignV.Center, 3, 0, colSpan: 2);
            descriptionText.OnLinkClicked += (x, url) => MyGuiSandbox.OpenUrl(url, UrlOpenMode.SteamOrExternalWithConfirm);
            plugin.GetDescriptionText(descriptionText);

            MyGuiControlCheckbox enabledCheckbox = new MyGuiControlCheckbox(toolTip: "Enabled", isChecked: enabledPlugins.Contains(plugin.Id));
            enabledCheckbox.IsCheckedChanged += OnEnabledChanged;
            layout.Add(enabledCheckbox, MyAlignH.Right, MyAlignV.Top, 0, 1);

            if (!plugin.IsLocal)
            {
                layout.Add(new MyGuiControlLabel(text: stats.Players + " users"), MyAlignH.Left, MyAlignV.Center, 2, 0);

                votingPanel = new MyGuiControlParent();
                layout.AddWithSize(votingPanel, MyAlignH.Center, MyAlignV.Center, 1, 1, 2);
                CreateVotingPanel(votingPanel);
            }

        }

        private void OnEnabledChanged(MyGuiControlCheckbox checkbox)
        {
            plugin.UpdateEnabledPlugins(enabledPlugins, checkbox.IsChecked);
        }

        private void CreateVotingPanel(MyGuiControlParent parent)
        {
            bool canVote = plugin.Enabled || stats.Tried;

            MyLayoutHorizontal layout = new MyLayoutHorizontal(parent, 0);

            MyGuiControlButton btnVoteUp = new MyGuiControlButton(visualStyle: MyGuiControlButtonStyleEnum.SquareSmall)
            {
                Checked = stats.Vote > 0,
            };
            if (canVote)
                btnVoteUp.ButtonClicked += OnRateUpClicked;
            else
                btnVoteUp.Enabled = false;
            AddImageToButton(btnVoteUp, @"Textures\GUI\Icons\Blueprints\like_test.png", 0.8f);
            layout.Add(btnVoteUp, MyAlignV.Bottom);

            MyGuiControlLabel lblVoteUp = new MyGuiControlLabel(text: stats.Upvotes.ToString());
            PositionToRight(btnVoteUp, lblVoteUp, spacing: GuiSpacing / 5);
            AdvanceLayout(ref layout, lblVoteUp.Size.X + GuiSpacing);
            parent.Controls.Add(lblVoteUp);

            MyGuiControlButton btnVoteDown = new MyGuiControlButton(visualStyle: MyGuiControlButtonStyleEnum.SquareSmall)
            {
                Checked = stats.Vote < 0,
            };
            if (canVote)
                btnVoteDown.ButtonClicked += OnRateDownClicked;
            else
                btnVoteDown.Enabled = false;
            AddImageToButton(btnVoteDown, @"Textures\GUI\\Icons\Blueprints\dislike_test.png", 0.8f);
            layout.Add(btnVoteDown, MyAlignV.Bottom);

            MyGuiControlLabel lblVoteDown = new MyGuiControlLabel(text: stats.Downvotes.ToString());
            PositionToRight(btnVoteDown, lblVoteDown, spacing: GuiSpacing / 5);
            parent.Controls.Add(lblVoteDown);
        }

        private void OnRateDownClicked(MyGuiControlButton btn)
        {
            Vote(-1);
        }

        private void OnRateUpClicked(MyGuiControlButton btn)
        {
            Vote(1);
        }

        private void Vote(int vote)
        {
            if (PlayerConsent.ConsentGiven)
                StoreVote(vote);
            else
                PlayerConsent.ShowDialog(() => StoreVote(vote));
        }

        private void StoreVote(int vote)
        {
            if (!PlayerConsent.ConsentGiven)
                return;

            if (stats.Vote == vote)
                vote = 0;

            PluginStat updatedStat = StatsClient.Vote(plugin.Id, vote);
            if (updatedStat == null)
                return;

            PluginStats allStats = Main.Instance.Stats;
            if (allStats != null)
                allStats.Stats[plugin.Id] = updatedStat;

            stats = updatedStat;
            RefreshVotingPanel();
        }

        private void RefreshVotingPanel()
        {
            if (votingPanel == null)
                return;
            votingPanel.Controls.Clear();
            CreateVotingPanel(votingPanel);
        }

        private void OnPluginSettingsClick(MyGuiControlButton btn)
        {
            if (pluginInstance != null)
                pluginInstance.OpenConfig();
        }

        private void OnPluginOpenClick(MyGuiControlButton btn)
        {
            plugin.Show();
        }

        public void InvokeOnPluginRemoved(PluginData plugin)
        {
            OnPluginRemoved?.Invoke(plugin);
        }

        public void InvokeOnRestartRequired()
        {
            OnRestartRequired?.Invoke();
        }
    }
}
