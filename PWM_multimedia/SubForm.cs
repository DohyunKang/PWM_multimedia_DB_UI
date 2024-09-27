using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;  // DataTable을 사용하기 위한 네임스페이스
using System.Windows.Forms;  // DataGridView 및 MessageBox 사용
using System.Data.SqlServerCe;  // SqlServerCe 관련 클래스 사용

namespace PWM_multimedia
{
    public class SubForm : Form1
    {
        private DataGridView dataGridView1;  // DataGridView를 선언

        public SubForm()
        {
            InitializeComponent();
            LoadDatabaseData();  // 데이터 로드 메서드를 호출
        }

        // SubForm에 필요한 UI 구성 요소 초기화
        private void InitializeComponent()
        {
            this.dataGridView1 = new DataGridView();

            // DataGridView 설정
            this.dataGridView1.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridView1.Location = new System.Drawing.Point(350, 550);
            this.dataGridView1.Name = "dataGridView1";
            this.dataGridView1.Size = new System.Drawing.Size(560, 400);
            this.dataGridView1.TabIndex = 0;

            // SubForm 설정
            this.ClientSize = new System.Drawing.Size(600, 450);
            this.Controls.Add(this.dataGridView1);  // DataGridView를 SubForm에 추가
            this.Name = "SubForm";
            this.Text = "Database Viewer";
        }

        // 데이터베이스에서 데이터를 로드하고 DataGridView에 표시
        private void LoadDatabaseData()
        {
            try
            {
                using (SqlCeConnection conn = new SqlCeConnection(@"Data Source = C:\Users\kangdohyun\Desktop\세미나\2주차\PWM_multimedia\MyDatabase#1.sdf"))
                {
                    conn.Open();
                    string query = "SELECT * FROM PwmData";  // PwmData 테이블에서 데이터를 조회
                    SqlCeCommand cmd = new SqlCeCommand(query, conn);
                    SqlCeDataReader reader = cmd.ExecuteReader();

                    // 데이터를 읽어서 테이블 형태로 변환
                    DataTable dataTable = new DataTable();
                    dataTable.Load(reader);
                    dataGridView1.DataSource = dataTable;  // DataGridView에 데이터를 표시
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("데이터를 불러오는 중 오류 발생: " + ex.Message);
            }
        }
    }
}
