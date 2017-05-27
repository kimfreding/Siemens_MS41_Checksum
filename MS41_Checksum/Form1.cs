using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;


namespace MS41_Checksum
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();

            toolStripMenuItem3.Enabled = false;
            toolStripMenuItem2.Enabled = false;
        }

        public class sim
        {
            public static string file;
            public static double length;
            public static byte[] buffFull;
            public static byte[] chkblock1;
            public static int offset = 0;
            public static ushort initial = 0;
            public static bool chksumcorr = false;
            public static int NumberOfChecksumsCorrected = 0; 
        }

        
        private void Checksum()
        {
            try
            {
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    sim.length = new System.IO.FileInfo(this.ofd.FileName).Length; // calculate file lenght 
                    sim.buffFull = File.ReadAllBytes(ofd.FileName); // read full file
                    if (sim.length != 262144 && sim.length != 24576) // test if file has correct size
                    {
                        label1.Text = "Wrong filesize: " + sim.length;
                        return;
                    }
                    // make sure this is false in case user opens more than one files
                    richTextBox1.Text = "";
                    label1.Text = ofd.SafeFileName;
                    sim.chksumcorr = false;
                    sim.NumberOfChecksumsCorrected = 0;

                    if (sim.length == 262144)
                    {
                        if (ChecksumBootSector())
                            sim.NumberOfChecksumsCorrected++;
                        if (ChecksumProgram())
                            sim.NumberOfChecksumsCorrected++;

                    }

                    // Find MS41 Checksum 
                    byte[] ToFind = new byte[] { 0x4E, 0x00, 0xFF, 0xFF };
                    int Start = IndexOfBytes(sim.buffFull, ToFind, -0x1, (int)sim.length - 1);
                    if (Start == -1)
                    {
                        richTextBox1.Text += "No MS41 checksum in this file\r\n";
                        return;
                    }
                    //richTextBox1.Text += "Found MS41 checksum at: 0x" + Start.ToString("X5") + "\r\n";

                    // Find initial Value
                    byte[] Initial_Value = new byte[0x2];
                    Buffer.BlockCopy(sim.buffFull, Start + 0x0E, Initial_Value, 0x0, 0x2);
                    Array.Reverse(Initial_Value);
                    sim.initial = BitConverter.ToUInt16(Initial_Value, 0);
                    ushort num = BitConverter.ToUInt16(Initial_Value, 0);
                    //richTextBox1.Text += "Found initial Value: 0x" + sim.initial.ToString("X2") + "\r\n\r\n";


                    int[,] Checksums;
                    Checksums = new int[20, 5]; // declare array of 20 rows and 3 colums
                    // checksum number /checksum addr / checksum file /checksum calculated /checksum bytes
                    int StartZ = Start;
                    bool Evaluate = false;

                    for (int i = 1; i != 20; i++)
                    {
                        // Checksum Start
                        ushort SS = BitConverter.ToUInt16(sim.buffFull, StartZ);
                        if (SS == 0xFFFF) break; // break if end of checksum
                        Checksums[i, 1] = (Start + SS); // put checksum start adress into array

                        // Calculate checksums + store in array Checksums at colum 3
                        int ChkSumSize = ((Start + SS) - StartZ); // calculate how many checksum bytes
                        Crc16 crc = new Crc16();
                        byte[] chksumblock = new byte[ChkSumSize];
                        Buffer.BlockCopy(sim.buffFull, StartZ, chksumblock, 0x0, ChkSumSize);
                        Checksums[i, 3] = BitConverter.ToUInt16(crc.ComputeChecksumBytes(chksumblock, num), 0);


                        // Read checksum
                        //ushort RS = BitConverter.ToUInt16(sim.buffFull, Start + SS);
                        int RS = ReverseBytes(BitConverter.ToUInt16(sim.buffFull, Start + SS));
                        Checksums[i, 2] = RS; // put it into array

                        StartZ = (Start + SS + 2); // calculate adress for reading next start value

                        // Check if checksums are correct or not and update them in sim.buffull if wrong
                        if (Checksums[i, 2] == Checksums[i, 3])
                            Evaluate = true;
                        else
                        {
                            Evaluate = false;
                            byte[] update = new byte[2];
                            update = BitConverter.GetBytes(ReverseBytes((ushort)Checksums[i, 3]));
                            // copy back to array
                            //Array.Reverse(update);
                            Buffer.BlockCopy(update, 0, sim.buffFull, Checksums[i, 1], 2);

                            // make sure checksums gets corrected
                            sim.chksumcorr = true;
                            // update number of wrong checksums detected
                            sim.NumberOfChecksumsCorrected++;

                            // debug 
                            /*
                            ushort tempor = BitConverter.ToUInt16(update, 0);
                            MessageBox.Show(tempor.ToString("X2"),
                            "Important Message");
                            */
                        }

                        //  Update Richtextbox
                        richTextBox1.Text += "Checksum " + i.ToString("D2") + " at 0x" + Checksums[i, 1].ToString("X5") +
                        " Value: " + RS.ToString("X4") + " Comp: " + Checksums[i, 3].ToString("X4") +
                        " BlockSize: " + ChkSumSize.ToString("X3");

                        if (Evaluate)
                        {
                            richTextBox1.AppendText("\t(Checksum OK)\r\n");
                        }
                        else
                        {
                            richTextBox1.AppendText("\t(Checksum ERROR)\r\n");
                        }

                    }

                    // save file if checksums are updated
                    if (sim.chksumcorr)
                    {
                        // debug File.WriteAllBytes(Path.ChangeExtension(ofd.FileName, ".ChksumCorrected"), sim.buffFull);
                        richTextBox1.Text += "\r\n" + sim.NumberOfChecksumsCorrected.ToString() + " Checksum(s) Corrected!";
                        toolStripMenuItem3.Enabled = true;
                        toolStripMenuItem2.Enabled = true;
                    }
                    else
                        richTextBox1.Text += "\r\n" + sim.NumberOfChecksumsCorrected.ToString() + " Checksum(s) Corrected!";

                }

            }
            catch (Exception ex)
            {

                MessageBox.Show(ex.Message);
            }
            
            

        }

        public bool ChecksumBootSector()
        {
            // bootsector
            Crc16 crc = new Crc16();
            ushort chksumCalc = 0x4711; // hardcoded initial value
            int chksumFile = BitConverter.ToUInt16(sim.buffFull, 0x5C80);
            chksumCalc = crc.CCB(sim.buffFull, 0x4000, 0x5C14, chksumCalc);

            if (chksumFile != chksumCalc)
            {
                    byte[] update = new byte[0x2];
                    update = BitConverter.GetBytes(chksumCalc);
                    sim.buffFull[0x5C80] = update[0x00];
                    sim.buffFull[0x5C81] = update[0x01];

                    richTextBox1.Text += "Checksum Bootsector -- Value: " + chksumFile.ToString("X4") + " Comp: " + chksumCalc.ToString("X4") + "\t\t\t(Checksum ERROR)\r\n";
                    toolStripMenuItem3.Enabled = true;
                    toolStripMenuItem2.Enabled = true;
                    return true;
            }
            richTextBox1.Text += "Checksum Bootsector -- Value: " + chksumFile.ToString("X4") + " Comp: " + chksumCalc.ToString("X4") + "\t\t\t(Checksum OK)\r\n";
            return false;

        }

        private bool ChecksumProgram()
        {
            Crc16 crc = new Crc16();
            
            // bootsector
            ushort chksumCalc = 0x0;
            ushort chksumFile = 0x0;

            ushort initial = 0;
            byte[] Initial_Value = new byte[0x2];
            Buffer.BlockCopy(sim.buffFull, 0x6066, Initial_Value, 0x0, 0x2);
            Array.Reverse(Initial_Value);
            initial = BitConverter.ToUInt16(Initial_Value, 0);
            chksumCalc = BitConverter.ToUInt16(Initial_Value, 0);
            // label5.Text = chksumCalc.ToString("X4"); // initial value

            chksumFile = BitConverter.ToUInt16(sim.buffFull, 0x6050);
            // label3.Text = chksumFile.ToString("X4");
            // all ms41`s
            chksumCalc = crc.CCB(sim.buffFull, 0x06100, FindCheckEnd(sim.buffFull, 0x7FFF), chksumCalc);
            chksumCalc = crc.CCB(sim.buffFull, 0x00000, FindCheckEnd(sim.buffFull, 0x3FFF), chksumCalc);

            // must be 8 parts
            //chksumCalc = crc.CCB(sim.buffFull1, 0x20000, FindCheckEnd(sim.buffFull1, 0x3FFFF), chksumCalc);
            byte[] buf = new byte[0x20000]; // rearange memory for checksum calculation

            Buffer.BlockCopy(sim.buffFull, 0x24000, buf, 0x00000, 0x4000);
            Buffer.BlockCopy(sim.buffFull, 0x20000, buf, 0x04000, 0x4000);
            Buffer.BlockCopy(sim.buffFull, 0x2c000, buf, 0x08000, 0x4000);
            Buffer.BlockCopy(sim.buffFull, 0x28000, buf, 0x0C000, 0x4000);

            Buffer.BlockCopy(sim.buffFull, 0x34000, buf, 0x10000, 0x4000);
            Buffer.BlockCopy(sim.buffFull, 0x30000, buf, 0x14000, 0x4000);
            Buffer.BlockCopy(sim.buffFull, 0x3c000, buf, 0x18000, 0x4000);
            Buffer.BlockCopy(sim.buffFull, 0x38000, buf, 0x1C000, 0x4000);

            chksumCalc = crc.CCB(buf, 0x00000, FindCheckEnd(buf, 0x1FFFF), chksumCalc);


            if (chksumFile != chksumCalc)
            {
                byte[] update = new byte[0x2];
                update = BitConverter.GetBytes(chksumCalc);
                sim.buffFull[0x6050] = update[0x00];
                sim.buffFull[0x6051] = update[0x01];

                richTextBox1.Text += "Checksum ProgramSec -- Value: " + chksumFile.ToString("X4") + " Comp: " + chksumCalc.ToString("X4") + "\t\t\t(Checksum ERROR)\r\n" + "\r\n";
                toolStripMenuItem3.Enabled = true;
                toolStripMenuItem2.Enabled = true;
                return true;
            }

            richTextBox1.Text += "Checksum ProgramSec -- Value: " + chksumFile.ToString("X4") + " Comp: " + chksumCalc.ToString("X4") + "\t\t\t(Checksum OK)\r\n";
            
            return false;
        }

        public int FindCheckEnd(byte[] buffer, int start)
        {
            while (buffer[start] == 0xFF)
            {
                start--;
            }

            return start + 1;
        }

        public static UInt16 ReverseBytes(UInt16 value)
        {
            return (UInt16)((value & 0xFFU) << 8 | (value & 0xFF00U) >> 8);
        }

        public static int IndexOfBytes(byte[] array, byte[] pattern, int startIndex, int count)
        {
            int i = startIndex;
            int endIndex = count > 0 ? startIndex + count : array.Length;
            int fidx = 0;

            while (i++ < endIndex)
            {
                fidx = (array[i] == pattern[fidx]) ? ++fidx : 0;
                if (fidx == pattern.Length)
                {
                    return i - fidx + 1;
                }
            }
            return -1;
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            toolStripMenuItem3.Enabled = false;
            toolStripMenuItem2.Enabled = false;
            Checksum();
        }

        private void toolStripMenuItem1_Click(object sender, EventArgs e)
        {
            About idiot = new About();
            idiot.Show();
        }

        private void toolStripMenuItem2_Click(object sender, EventArgs e)
        {
            File.WriteAllBytes(ofd.FileName, sim.buffFull);
        }

        private void toolStripMenuItem3_Click(object sender, EventArgs e)
        {
            SaveFileDialog sfd = new SaveFileDialog();
            if (sfd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {

                File.WriteAllBytes(sfd.FileName,sim.buffFull);
                
            }
        }
       
    }
}
