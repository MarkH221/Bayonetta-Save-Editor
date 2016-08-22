using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Isolite.IOPackage;
using Isolite.PS3FileSystem;

namespace BayonettaSE
{
    public partial class Form1 : Form
    {
        /// <summary>
        ///     The target filename, typically inside a ps3 save folder there is usually one file of particular interest the rest
        ///     are background noise.
        /// </summary>
        private const string FileName = "CNTDAT";

        private byte[] _buffer;
        private string _filepath, _file;
        private bool _isps3 = true;
        private Ps3SaveManager _manager;
        private bool _predecrypted;

        /// <summary>
        ///     This is the PS3 savedata encryption key. I wonder why they bothered.
        /// </summary>
        private readonly byte[] _key =
        {
            0x41, 0x42, 0x43, 0x44, 0x45, 0x46, 0x46, 0x46, 0x46, 0x46, 0x46, 0x46, 0x46,
            0x46, 0x46, 0x00
        };

        public Form1()
        {
            InitializeComponent();
            difficultybox.SelectedIndex = 0;
        }

        private void Read()
        {
            //Read operation.
            using (var r = new RWStream(_buffer, true))
            {
                //Difficulty
                r.Position = 0x33;
                difficultybox.SelectedIndex = r.ReadInt8();
                //Halos
                r.Position = 0x0000EF44;
                var t = r.ReadUInt32();
                halobox.Value = (t > Int32.MaxValue) ? Int32.MaxValue : t;
            }
        }

        private void PS3Open(object sender, EventArgs e)
        {
            using (var o = new FolderBrowserDialog {Description = "Navigate to Bayonetta '-CNT' PS3 Save Folder"})
            {
                if (o.ShowDialog() != DialogResult.OK) return;
                _filepath = o.SelectedPath;
                _manager = new Ps3SaveManager(_filepath, _key);
                //Check for telltale sign of decryption via pfdtool or any files with broken hashes, sign of decryption or unfixed tampering.
                if (Directory.GetFiles(_filepath).Any(i => i.Contains("~files_decrypted_by_pfdtool")) ||
                    _manager.Files.Any(i => i.IsEncrypted == false))
                {
                    _predecrypted = true;
                    _file = _filepath + @"\" + FileName;
                    _buffer = File.ReadAllBytes(_file);
                }
                else
                {
                    _predecrypted = false;
                    var files = _manager.Files.FirstOrDefault(t => t.PFDEntry.file_name == FileName);
                    if (files != null) _buffer = files.DecryptToBytes();
                }
                _isps3 = true;
            }

            Read();
        }

        private void Save(object sender, EventArgs e)
        {
            //New checksum variable.
            var c = 0;
            using (var r = new RWStream(_buffer, true))
            {
                //Halo
                r.Position = 0x0000EF44;
                r.WriteInt32((int) halobox.Value);
                //Write Items
                if (concheckbox.Checked)
                {
                    r.Position = 0x0000EF5A;
                    for (var i = 0; i < 16; i++)
                    {
                        r.WriteInt32(9999);
                    }
                }

                //Difficulty
                r.Position = 0x33;
                r.WriteUInt8((byte) difficultybox.SelectedIndex);

                //Bayonetta's checksum is located at 0x14 and is 4 bytes.
                //The checksum is for 0x18 -> EOF
                //Compute checksum
                r.Position = 0x18;
                do
                {
                    //XOR!
                    c ^= r.ReadInt32();
                } while (r.Position < _buffer.Length);
                //Write new checksum to buffer.
                r.Position = 0x14;
                r.WriteInt32(c);
            }
            //Write edited buffer to file.
            File.WriteAllBytes(_file, _buffer);
            if (_isps3)
            {
                //Rebuild & Encrypt if was decrypted in editor, otherwise leave to whatever tool user used.
                if (!_predecrypted) _manager.ReBuildChanges(true);
            }
            MessageBox.Show("Saving Complete.\n New Checksum: 0x" + c.ToString("X8"));
        }

        private void WiiUOpen(object sender, EventArgs e)
        {
            using (var o = new OpenFileDialog())
            {
                if (o.ShowDialog() != DialogResult.OK) return;
                _file = o.FileName;
                _buffer = File.ReadAllBytes(_file);
                Read();
                _isps3 = false;
            }
        }

        private void MaxHalos(object sender, EventArgs e)
        {
            halobox.Value = int.MaxValue;
        }
    }
}