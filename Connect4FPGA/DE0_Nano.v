module DE0_Nano (
    input CLOCK_50, // internal FPGA clock (e.g. 50MHz)
    inout [33:0] GPIO_0
);

	 wire d_in = GPIO_0[1]; // serial data sent by pi
    wire master_clk = GPIO_0[0]; // clk from rpi
    wire cs = GPIO_0[3]; // cs line from pi
    wire d_out; // serial out
    wire out_enable; // 1 if fpga wants pi to raise cs

    reg [7:0] reset_counter = 0;
    reg auto_reset = 1'b0;

    // Auto-reset logic
    always @(posedge CLOCK_50) begin
        if (reset_counter < 100) begin
            reset_counter <= reset_counter + 1;
            auto_reset <= 1'b0;
        end else begin
            auto_reset <= 1'b1;
        end
    end

	 
	 assign GPIO_0[2] = d_out;
    assign GPIO_0[4] = out_enable;

	 
    // Final logic module
    FinalEvaluation finaleval (
        .clk(CLOCK_50),
        .master_clk(master_clk),
        .cs(cs),
        .final_evaluation_reset(auto_reset),
        .d_in(d_in),
        .d_out(d_out),
        .out_enable(out_enable)
    );

endmodule