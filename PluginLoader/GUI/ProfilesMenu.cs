﻿using avaness.PluginLoader.Data;
using Sandbox.Graphics.GUI;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRage.Utils;
using VRageMath;
using RectangleF = VRageMath.RectangleF;

namespace avaness.PluginLoader.GUI
{
    public class ProfilesMenu : PluginScreen
    {
        private MyGuiControlTable profilesTable;
        private MyGuiControlButton btnUpdate, btnLoad, btnRename, btnDelete;
        private Dictionary<string, Profile> profiles;
        private readonly HashSet<string> enabledPlugins;
        private bool profilesModified = false;

        public ProfilesMenu(HashSet<string> enabledPlugins) : base(size: new Vector2(0.85f, 0.52f))
        {
            this.enabledPlugins = enabledPlugins;
            profiles = Main.Instance.Config.ProfileMap;
            Closed += OnScreenClosed;
        }

        private void OnScreenClosed(MyGuiScreenBase source, bool isUnloading)
        {
            if(profilesModified)
                Main.Instance.Config.Save();
            Closed -= OnScreenClosed;
        }

        public override string GetFriendlyName()
        {
            return typeof(ProfilesMenu).FullName;
        }

        public override void RecreateControls(bool constructor)
        {
            base.RecreateControls(constructor);

            // Top
            MyGuiControlLabel caption = AddCaption("Profiles", captionScale: 1);
            AddBarBelow(caption);

            // Bottom: New/Update, Load, Rename, Delete
            Vector2 bottomMid = new Vector2(0, m_size.Value.Y / 2);
            btnLoad = new MyGuiControlButton(position: new Vector2(bottomMid.X - (GuiSpacing / 2), bottomMid.Y - GuiSpacing), text: new StringBuilder("Load"), originAlign: MyGuiDrawAlignEnum.HORISONTAL_RIGHT_AND_VERTICAL_BOTTOM, onButtonClick: OnLoadClick);

            btnUpdate = new MyGuiControlButton(text: new StringBuilder("New"), onButtonClick: OnUpdateClick);
            PositionToLeft(btnLoad, btnUpdate);

            btnRename = new MyGuiControlButton(text: new StringBuilder("Rename"), onButtonClick: OnRenameClick);
            PositionToRight(btnLoad, btnRename);

            btnDelete = new MyGuiControlButton(text: new StringBuilder("Delete"), onButtonClick: OnDeleteClick);
            PositionToRight(btnRename, btnDelete);

            Controls.Add(btnUpdate);
            Controls.Add(btnLoad);
            Controls.Add(btnRename);
            Controls.Add(btnDelete);
            AddBarAbove(btnLoad);

            // Table
            RectangleF area = GetAreaBetween(caption, btnRename, GuiSpacing * 2);

            profilesTable = new MyGuiControlTable()
            {
                Size = area.Size,
                Position = area.Position,
                OriginAlign = MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP,
            };
            profilesTable.ColumnsCount = 2;
            profilesTable.SetCustomColumnWidths(new[]
            {
                0.4f,
                0.6f,
            });
            profilesTable.SetColumnName(0, new StringBuilder("Name"));
            profilesTable.SetColumnName(1, new StringBuilder("Enabled Count"));
            profilesTable.ItemDoubleClicked += OnItemDoubleClicked;
            profilesTable.ItemSelected += OnItemSelected;
            SetTableHeight(profilesTable, area.Size.Y);
            Controls.Add(profilesTable);
            foreach (Profile p in profiles.Values)
                profilesTable.Add(CreateProfileRow(p));
            UpdateButtons();
        }

        private void OnItemSelected(MyGuiControlTable table, MyGuiControlTable.EventArgs args)
        {
            UpdateButtons();
        }

        private void OnItemDoubleClicked(MyGuiControlTable table, MyGuiControlTable.EventArgs args)
        {
            int rowIndex = args.RowIndex;
            if (rowIndex >= 0 && rowIndex < table.RowsCount && table.GetRow(rowIndex)?.UserData is Profile p)
                LoadProfile(p);
        }

        private void LoadProfile(Profile p)
        {
            enabledPlugins.Clear();
            foreach(PluginData plugin in p.GetPlugins())
                enabledPlugins.Add(plugin.Id);
            CloseScreen();
        }

        private static MyGuiControlTable.Row CreateProfileRow(Profile p)
        {
            MyGuiControlTable.Row row = new MyGuiControlTable.Row(p);

            row.AddCell(new MyGuiControlTable.Cell(text: p.Name, toolTip: p.Name));
            string desc = p.GetDescription();
            row.AddCell(new MyGuiControlTable.Cell(text: desc, toolTip: desc));
            return row;
        }

        private void UpdateButtons()
        {
            bool selected = profilesTable.SelectedRow != null;
            btnUpdate.Text = selected ? "Update" : "New";
            btnLoad.Enabled = selected;
            btnRename.Enabled = selected;
            btnDelete.Enabled = selected;
        }

        private void OnDeleteClick(MyGuiControlButton btn)
        {
            MyGuiControlTable.Row row = profilesTable.SelectedRow;
            if (row?.UserData is Profile p)
            {
                profiles.Remove(p.Key);
                profilesTable.Remove(row);
                profilesModified = true;
                UpdateButtons();
            }
        }

        private void OnRenameClick(MyGuiControlButton btn)
        {
            MyGuiControlTable.Row row = profilesTable.SelectedRow;
            if (row?.UserData is Profile p)
            {
                MyScreenManager.AddScreen(new TextInputDialog("Profile Name", p.Name, onComplete: (name) =>
                {
                    p.Name = name;
                    row.GetCell(0).Text.Clear().Append(name);
                    profilesModified = true;
                }));
            }
        }

        private void OnLoadClick(MyGuiControlButton btn)
        {
            if (profilesTable.SelectedRow?.UserData is Profile p)
                LoadProfile(p);
        }


        private void OnUpdateClick(MyGuiControlButton btn)
        {
            MyGuiControlTable.Row row = profilesTable.SelectedRow;
            if(row == null)
            {
                // New profile
                MyScreenManager.AddScreen(new TextInputDialog("Profile Name", onComplete: CreateProfile));
            }
            else if(row.UserData is Profile p)
            {
                // Update profile
                p.Plugins = enabledPlugins.ToArray();
                row.GetCell(1).Text.Clear().Append(p.GetDescription());
                profilesModified = true;
            }
        }

        private void CreateProfile(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return;

            Profile newProfile = new Profile(name, enabledPlugins.ToArray());
            profiles[newProfile.Key] = newProfile;
            MyGuiControlTable.Row row = CreateProfileRow(newProfile);
            profilesTable.Add(row);
            profilesTable.SelectedRow = row;
            UpdateButtons();
            profilesModified = true;
        }

    }
}
