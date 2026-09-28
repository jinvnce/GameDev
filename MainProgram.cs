namespace Game
{
    public static class MainProgram
    {
        [STAThread]
        public static void Main()
        {

            ApplicationConfiguration.Initialize();
            Application.Run(new MenuForm());
        }
    }
}