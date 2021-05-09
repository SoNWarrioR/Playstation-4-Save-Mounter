using System;

namespace PS4Saves.Form.UnmountAll
{
    public partial class UnmountAllTypeDialog : System.Windows.Forms.Form
    {
        private Main mainForm = null;
        public UnmountAllTypeDialog()
        {
            InitializeComponent();
        }
        
        public UnmountAllTypeDialog(System.Windows.Forms.Form callingForm)
        {
            InitializeComponent();
            mainForm = callingForm as Main; 
        }

        public void tryDirtyUnmount(object sender, EventArgs e)
        {
            this.mainForm?.TryDirtyUnmount();
        }

        public void tryUnmountExists(object sender, EventArgs e)
        {
            this.mainForm?.TryUnmountExists();
        }

    }
}