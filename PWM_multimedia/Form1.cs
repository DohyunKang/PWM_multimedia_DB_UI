using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using NationalInstruments.DAQmx;
using System.Collections.Generic;
using System.Data.SqlServerCe;

namespace PWM_multimedia
{
    public partial class Form1 : Form
    {
        private NationalInstruments.DAQmx.Task writeTask;
        private NationalInstruments.DAQmx.Task analogReadTask;
        private AnalogSingleChannelWriter writer;
        private AnalogSingleChannelReader analogReader;

        private double outputVoltage;
        private double inputVoltage;
        private double frequency;
        private double dutyCycle;
        private bool pwmStateHigh;
        private double highTime;
        private double lowTime;
        private double pwmElapsed;

        private double HighV;
        private double LowV;

        private DateTime lastPwmTime = DateTime.Now;
        private DateTime cycleStartTime = DateTime.Now;

        private double previousVoltage = 0;
        private DateTime lastEdgeTime = DateTime.Now;

        private bool flag = false;
        private int pwmIndex = 0; // 프로그램 종료 시 0로 초기화됨
        private int currentPwmId = 0; // 자동 증가되는 p_index 관리

        public Form1()
        {
            InitializeComponent();
            Init();
        }

        public void Init()
        {
            writeTask = new NationalInstruments.DAQmx.Task();
            analogReadTask = new NationalInstruments.DAQmx.Task();

            writeTask.AOChannels.CreateVoltageChannel("Dev1/ao0", "", 0.0, 5.0, AOVoltageUnits.Volts);
            analogReadTask.AIChannels.CreateVoltageChannel("Dev1/ai0", "", AITerminalConfiguration.Rse, 0.0, 5.0, AIVoltageUnits.Volts);

            writer = new AnalogSingleChannelWriter(writeTask.Stream);
            analogReader = new AnalogSingleChannelReader(analogReadTask.Stream);

            frequency = 50;
            dutyCycle = 50;
            HighV = 5;
            LowV = 0;

            UpdatePWMParameters();
        }

        // 매개변수가 없는 기본 파라미터 업데이트 메서드
        private void UpdatePWMParameters()
        {
            UpdatePWMParameters(frequency, dutyCycle, HighV, LowV);  // 기존 메서드 호출
        }

        private void StartMultimediaTimer()
        {
            System.Threading.Tasks.Task.Run(() => GeneratePWMAndReadAIAsync());
        }

        private void StopMultimediaTimer()
        {
            flag = false;
        }

        private async System.Threading.Tasks.Task GeneratePWMAndReadAIAsync()
        {
            flag = true;
            lastPwmTime = DateTime.Now; // 시작할 때 시간을 초기화

            double totalHighTime = 0;  // 누적 high time을 저장
            double totalLowTime = 0;   // 누적 low time을 저장
            DateTime highStartTime = DateTime.Now;
            DateTime lowStartTime = DateTime.Now;

            while (flag)
            {
                DateTime currentTime = DateTime.Now;
                TimeSpan deltaTime = currentTime - lastPwmTime;
                double elapsedSeconds = deltaTime.TotalSeconds;

                pwmElapsed += elapsedSeconds;
                lastPwmTime = currentTime;

                // PWM 신호 생성 (고정된 파라미터로 신호를 만듦)
                if (pwmStateHigh && pwmElapsed >= highTime)
                {
                    writer.WriteSingleSample(true, LowV);
                    pwmStateHigh = false;
                    pwmElapsed = 0;
                    lowStartTime = DateTime.Now;  // Low 상태 시작 시점 기록
                }
                else if (!pwmStateHigh && pwmElapsed >= lowTime)
                {
                    writer.WriteSingleSample(true, HighV);
                    pwmStateHigh = true;
                    pwmElapsed = 0;
                    highStartTime = DateTime.Now;  // High 상태 시작 시점 기록
                }

                // AI 신호 측정 (실제 신호에서 rising/falling edge 탐지)
                double inputVoltage = analogReader.ReadSingleSample();

                // Rising edge 감지: low에서 high로 변환될 때
                if (previousVoltage <= (LowV + 0.1) && inputVoltage >= (HighV - 0.1))
                {
                    // Falling edge가 있었을 경우 low time 계산
                    if (lowStartTime != DateTime.MinValue)
                    {
                        TimeSpan lowTimeSpan = currentTime - lowStartTime;
                        totalLowTime = lowTimeSpan.TotalMilliseconds;
                    }

                    highStartTime = currentTime;  // Rising edge에서 high 시작
                }
                // Falling edge 감지: high에서 low로 변환될 때
                else if (previousVoltage >= (HighV - 0.1) && inputVoltage <= (LowV + 0.1))
                {
                    // Rising edge가 있었을 경우 high time 계산
                    if (highStartTime != DateTime.MinValue)
                    {
                        TimeSpan highTimeSpan = currentTime - highStartTime;
                        totalHighTime = highTimeSpan.TotalMilliseconds;
                    }

                    lowStartTime = currentTime;  // Falling edge에서 low 시작
                }

                // 실제 주기와 듀티 계산
                if (totalHighTime > 0 && totalLowTime > 0)
                {
                    double period = totalHighTime + totalLowTime;  // 주기 계산
                    double actualDutyCycle = (totalHighTime / period) * 100;  // 듀티 사이클 계산
                    double calculatedFrequency = 1000 / period;  // 주파수 계산

                    // UI에 표시
                    this.Invoke((MethodInvoker)delegate
                    {
                        lblPeriod.Text = period.ToString("F2");
                        lblFrequency.Text = calculatedFrequency.ToString("F2");
                        lblDuty.Text = actualDutyCycle.ToString("F2");
                    });
                }

                previousVoltage = inputVoltage;

                // 실시간 그래프 업데이트
                this.Invoke((MethodInvoker)delegate
                {
                    ContinuousWfg.PlotYAppend(inputVoltage, elapsedSeconds);
                });

                await System.Threading.Tasks.Task.Delay(1);
            }
        }

        private void UpdatePWMParameters(double frequency, double dutyCycle, double HighV, double LowV)
        {
            double period = 1.0 / frequency;
            highTime = period * (dutyCycle / 100.0);
            lowTime = period - highTime;
            this.HighV = HighV;
            this.LowV = LowV;
            pwmElapsed = 0;
            pwmStateHigh = true;
        }

        private void ApplyButton_Click_1(object sender, EventArgs e)
        {
            try
            {
                double frequency, dutyCycle, HighV, LowV;

                frequency = 1000 / (double)PeriodEdit.Value;
                dutyCycle = (double)DutyEdit.Value;
                HighV = (double)HighEdit.Value;
                LowV = (double)LowEdit.Value;

                lblPeriod.Text = (1000 / frequency).ToString("F2");
                lblFrequency.Text = frequency.ToString("F2");
                lblDuty.Text = dutyCycle.ToString("F2");

                UpdatePWMParameters(frequency, dutyCycle, HighV, LowV);

                // Apply할 때마다 데이터베이스에 저장
                pwmIndex++; // Apply할 때마다 증가
                currentPwmId = InsertPwmDataToDatabase((float)PeriodEdit.Value, (float)frequency, (float)HighEdit.Value, (float)DutyEdit.Value, 1, pwmIndex);

                MessageBox.Show("파라미터 업데이트 및 데이터 저장 완료");
            }
            catch (Exception ex)
            {
                MessageBox.Show("파라미터 업데이트 중 오류 발생: " + ex.Message);
            }
        }

        private void CaptureButton_Click_1(object sender, EventArgs e)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<object, EventArgs>(CaptureButton_Click_1), sender, e);
                return;
            }

            // 실시간 계산된 주기 및 듀티 사이클 값 사용 (캡처 버튼을 누른 당시의 값)
            double capturedPeriod = double.Parse(lblPeriod.Text);  // 캡처 버튼을 눌렀을 때의 실시간 주기
            double capturedDutyCycle = double.Parse(lblDuty.Text); // 캡처 버튼을 눌렀을 때의 실시간 듀티 사이클

            // highTime과 lowTime 계산 (실시간 측정값 기반)
            double capturedHighTime = (capturedDutyCycle / 100.0) * capturedPeriod;
            double capturedLowTime = capturedPeriod - capturedHighTime;

            // 실시간 데이터 기반의 PWM 신호 생성
            List<double> capturedSignal = new List<double>();
            double timeStep = 1.0; // 타임스텝(해상도)

            // 한 주기의 PWM 신호 생성
            for (double t = 0; t < capturedPeriod; t += timeStep)
            {
                if (t < capturedHighTime)
                {
                    capturedSignal.Add(HighV);  // High 상태 (실시간 주기 및 듀티 사이클 기반)
                }
                else
                {
                    capturedSignal.Add(LowV);   // Low 상태 (실시간 주기 및 듀티 사이클 기반)
                }
            }

            // 캡처된 신호를 그래프에 표시
            if (capturedSignal.Count > 0)
            {
                CaptureWfg.ClearData();  // 기존 데이터를 지우고
                CaptureWfg.PlotY(capturedSignal.ToArray());  // 새로운 실시간 측정 기반 신호 그리기

                // 캡처 시점의 p_index 값을 c_index로 참조
                int pIndexFromDatabase = GetLastInsertedPwmIndex();
                InsertCalculateDataToDatabase((float)capturedPeriod, (float)(1000 / capturedPeriod), (float)HighV, (float)capturedDutyCycle, pIndexFromDatabase, pwmIndex);
            }

            // MessageBox로 실시간 측정값과 설정값 비교
            MessageBox.Show(string.Format("실시간 Period: {0}\n실시간 DutyCycle: {1}\ncaptured High Time: {2}",
                capturedPeriod, capturedDutyCycle, capturedHighTime), "Captured Info");
        }

        private int GetLastInsertedPwmIndex()
        {
            try
            {
                using (SqlCeConnection conn = new SqlCeConnection(@"Data Source = C:\Users\kangdohyun\Desktop\세미나\2주차\PWM_multimedia\MyDatabase#1.sdf"))
                {
                    conn.Open();

                    string query = "SELECT MAX(p_index) FROM PwmData";
                    using (SqlCeCommand cmd = new SqlCeCommand(query, conn))
                    {
                        object result = cmd.ExecuteScalar();
                        return (int)(result ?? 0);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("p_index 조회 중 오류 발생: " + ex.Message);
                return -1;
            }
        }

        private int InsertPwmDataToDatabase(float period, float frequency, float voltage, float duty, int switchState, int pwmIndex)
        {
            try
            {
                using (SqlCeConnection conn = new SqlCeConnection(@"Data Source = C:\Users\kangdohyun\Desktop\세미나\2주차\PWM_multimedia\MyDatabase#1.sdf"))
                {
                    conn.Open();

                    string query = "INSERT INTO PwmData (Period, Frequency, Voltage, Duty, Switch, Time, pwmIndex) VALUES (@Period, @Frequency, @Voltage, @Duty, @Switch, @Time, @pwmIndex)";
                    using (SqlCeCommand cmd = new SqlCeCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Period", period);
                        cmd.Parameters.AddWithValue("@Frequency", frequency);
                        cmd.Parameters.AddWithValue("@Voltage", voltage);
                        cmd.Parameters.AddWithValue("@Duty", duty);
                        cmd.Parameters.AddWithValue("@Switch", switchState);
                        cmd.Parameters.AddWithValue("@Time", lastPwmTime);
                        cmd.Parameters.AddWithValue("@pwmIndex", pwmIndex); // Apply 누를 때 증가하는 pwmIndex 저장

                        cmd.ExecuteNonQuery();
                    }

                    return GetLastInsertedPwmIndex();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("데이터 삽입 중 오류 발생: " + ex.Message);
                return -1;
            }
        }

        private int InsertCalculateDataToDatabase(float c_period, float c_frequency, float c_voltage, float c_duty, int p_index, int pwmIndex)
        {
            try
            {
                using (SqlCeConnection conn = new SqlCeConnection(@"Data Source = C:\Users\kangdohyun\Desktop\세미나\2주차\PWM_multimedia\MyDatabase#1.sdf"))
                {
                    conn.Open();

                    string query = "INSERT INTO Calculate (C_Period, C_Frequency, C_Voltage, C_Duty, Time, c_index, pwmIndex) VALUES (@C_Period, @C_Frequency, @C_Voltage, @C_Duty, @Time, @c_index, @pwmIndex)";
                    using (SqlCeCommand cmd = new SqlCeCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@C_Period", c_period);
                        cmd.Parameters.AddWithValue("@C_Frequency", c_frequency);
                        cmd.Parameters.AddWithValue("@C_Voltage", c_voltage);
                        cmd.Parameters.AddWithValue("@C_Duty", c_duty);
                        cmd.Parameters.AddWithValue("@Time", lastPwmTime);
                        cmd.Parameters.AddWithValue("@c_index", p_index); // c_index가 p_index를 참조
                        cmd.Parameters.AddWithValue("@pwmIndex", pwmIndex); // pwmIndex도 저장

                        cmd.ExecuteNonQuery();
                    }

                    return p_index;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Calculate 데이터 삽입 중 오류 발생: " + ex.Message);
                return -1;
            }
        }

        private void ResetButton_Click(object sender, EventArgs e)
        {
            StopMultimediaTimer();
            writer.WriteSingleSample(true, 0);
            ContinuousWfg.ClearData();
        }

        private void switch1_StateChanged(object sender, EventArgs e)
        {
            if (!flag)
            {
                StartMultimediaTimer(); // 타이머 시작
                flag = true; // PWM 신호를 활성화 상태로 설정
            }
            else
            {
                StopMultimediaTimer(); // 타이머 중지
                flag = false; // PWM 신호를 비활성화 상태로 설정
            }
        }

        private void ShowDbButton_Click(object sender, EventArgs e)
        {
            try
            {
                this.Hide();  // Form1을 잠시 숨김
                SubForm subForm = new SubForm();
                subForm.ShowDialog();  // 모달로 SubForm을 띄움
            }
            finally
            {
                this.Show();  // SubForm이 닫히면 다시 Form1을 표시
            }
        }
    }
}
