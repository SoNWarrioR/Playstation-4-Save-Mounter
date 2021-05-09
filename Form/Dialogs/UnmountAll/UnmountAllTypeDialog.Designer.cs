﻿using System.ComponentModel;
using System.Windows.Forms;

namespace PS4Saves.Form.Dialogs.UnmountAll
{
    partial class UnmountAllTypeDialog
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }

            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        
        private System.Windows.Forms.Button dirtyUnmount;
        private System.Windows.Forms.Button unmountExists;
        private void InitializeComponent()
        {
            this.dirtyUnmount = new System.Windows.Forms.Button();
            this.unmountExists = new System.Windows.Forms.Button();


            #region dryUnmount Button

            this.dirtyUnmount.Location = new System.Drawing.Point(5, 5);
            this.dirtyUnmount.Name = "dirtyUnmount";
            this.dirtyUnmount.Size = new System.Drawing.Size(181, 25);
            this.dirtyUnmount.TabIndex = 0;
            this.dirtyUnmount.Text = "Dry Unmount all";
            this.dirtyUnmount.UseVisualStyleBackColor = true;
            this.dirtyUnmount.Click += new System.EventHandler(this.tryDirtyUnmount);

            #endregion
            
            #region tryUnmountExists Button

            this.unmountExists.Location = new System.Drawing.Point(5, 30);
            this.unmountExists.Name = "unmountExists";
            this.unmountExists.Size = new System.Drawing.Size(181, 25);
            this.unmountExists.TabIndex = 1;
            this.unmountExists.Text = "Unmount all exists";
            this.unmountExists.UseVisualStyleBackColor = true;
            this.unmountExists.Click += new System.EventHandler(this.tryUnmountExists);

            #endregion
            
            this.components = new System.ComponentModel.Container();
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ClientSize = new System.Drawing.Size(190, 60);
            this.Text = "Select unmount type";
            this.Name = "UnmountAllTypeDialog";

            #region Controls

            this.Controls.Add(this.dirtyUnmount);
            this.Controls.Add(this.unmountExists);
            
            #endregion
        }

        #endregion
    }
}