`timescale 1ns/1ps

module FinalEvaluation_tb();

reg clk;
reg master_clk;
reg cs;
reg final_evaluation_reset;
reg d_in;
wire d_out;
wire out_enable;

reg [31:0] received_evaluation;
reg [7:0] rpi_is_max_or_min;
reg [7:0] rpi_batch_size;
reg [87:0] rpi_batch;
reg [631:0] rpi_data;
wire final_evaluation_stable;
wire receive_or_send_data;
wire [7:0] batch_size;
integer i;

initial begin
    clk = 0;
    forever #2 clk <= ~clk; // 125 MHz
end

initial begin
    master_clk = 0;
    forever #13 master_clk <= ~master_clk; // ~71 MHz — not a multiple of clk
end

FinalEvaluation uut(
	.clk(clk),
	.master_clk(master_clk),
	.cs(cs),
	.final_evaluation_reset(final_evaluation_reset),
	.d_in(d_in),
	.d_out(d_out),
	.out_enable(out_enable),
	.final_evaluation_stable(final_evaluation_stable),
	.receive_or_send_data(receive_or_send_data),
	.batch_size(batch_size)

);

initial begin
	received_evaluation <= 0; //initialize the rpi received evaluation
	final_evaluation_reset = 0; //reset the finalevaluation instance
   #20 final_evaluation_reset = 1; //release the reset
	
	#10 cs <= 1'd1; //rpi cs is held high
	d_in <= 1'd0; //initialize serial data
	rpi_is_max_or_min <= 8'd1; //set is_max_or_min as max
	rpi_batch_size <= 8'd1; //batch size is one board
	
	
	
	//rpi_batch <= {4'b1010, {608{1'b1}}, 4'b0101};
	/*
	rpi_batch <= {
    //448'd0, // padding in upper bits [615:168]
		4'd0, //padding
    // Row 0 (top)
    2'd1, 2'd1, 2'd1, 2'd1, 2'd0, 2'd0, 2'd0,

    // Row 1
    2'd0, 2'd2, 2'd0, 2'd0, 2'd0, 2'd0, 2'd0,

    // Row 2
    2'd0, 2'd2, 2'd0, 2'd1, 2'd0, 2'd0, 2'd0,

    // Row 3
    2'd0, 2'd0, 2'd2, 2'd0, 2'd1, 2'd0, 2'd0,

    // Row 4
    2'd0, 2'd0, 2'd0, 2'd2, 2'd0, 2'd1, 2'd0,

    // Row 5 (bottom)
    2'd0, 2'd0, 2'd0, 2'd0, 2'd2, 2'd0, 2'd1
	 //4'd0 //padding
	};
	*/
	
	//batch being sent
	rpi_batch <= {
    4'd0, // padding

    // Row 0 (top)
    2'd0, 2'd0, 2'd1, 2'd2, 2'd0, 2'd0, 2'd0,

    // Row 1
    2'd0, 2'd1, 2'd2, 2'd1, 2'd2, 2'd0, 2'd0,

    // Row 2
    2'd1, 2'd2, 2'd1, 2'd2, 2'd1, 2'd0, 2'd0,

    // Row 3
    2'd2, 2'd1, 2'd2, 2'd1, 2'd2, 2'd1, 2'd0,

    // Row 4
    2'd1, 2'd2, 2'd1, 2'd2, 2'd1, 2'd2, 2'd1,

    // Row 5 (bottom)
    2'd2, 2'd1, 2'd2, 2'd1, 2'd2, 2'd1, 2'd2
};


	//combine all the data together
	#10 //rpi_data <= {rpi_is_max_or_min, rpi_batch_size, rpi_batch};
	rpi_data <= {
    632'b00000001000001110000000000000000000000000000000001000000000000100000000000001000000000000001010010000000000000000000000000000000000000000000000000000010000000000000100100000000000101001000000000000000000000000000000000000000000000000000001000000000000010000000000000010101100000000000000000000000000000000000000000000000000000100000000000001000000100000001010010000000000000000000000000000000000000000000000000000010000000000000100000000000000101001001000000000000000000000000000000000000000000000000001000000000000010000000000000010100100001000000000000000000000000000000000000000000000000100000000000001000000000000001010010000001
	};

	//rpi cs is brought low to send data
	#10 cs <= 1'd0;
	
	//data is received
	//@(posedge master_clk);
	for (i = 632; i >= 0; i = i - 1) begin
		@(negedge master_clk);
		d_in <= rpi_data[i];
	end
	
	//cs is brought high again after data is sent
	@(posedge master_clk);
	cs <= 1'd1;
	
	//waits until fpga is ready to send evaluation back and then brings cs low to receive data
	wait(out_enable == 1);
	cs <= 0;
	
	@(posedge master_clk);

	//sends the evaluation
	for (i = 0; i < 32; i = i + 1) begin
		 @(posedge master_clk);
		 received_evaluation <= {received_evaluation[30:0], d_out};  // shift in MSB-first
	end
	
	//brings cs high again when fpga is done sending data
	wait(out_enable == 0);
	@(posedge master_clk);
	cs <= 1'd1;
	
	#40;
	
	
	
	
	//SECOND TIME AROUND
	//rpi cs is brought low to send data
	#10 cs <= 1'd0;
	
	//data is received
	//@(posedge master_clk);
	for (i = 632; i >= 0; i = i - 1) begin
		@(negedge master_clk);
		d_in <= rpi_data[i];
	end
	
	//cs is brought high again after data is sent
	@(posedge master_clk);
	cs <= 1'd1;
	
	//waits until fpga is ready to send evaluation back and then brings cs low to receive data
	wait(out_enable == 1);
	cs <= 0;
	
	@(posedge master_clk);

	//sends the evaluation
	for (i = 0; i < 32; i = i + 1) begin
		 @(posedge master_clk);
		 received_evaluation <= {received_evaluation[30:0], d_out};  // shift in MSB-first
	end
	
	//brings cs high again when fpga is done sending data
	wait(out_enable == 0);
	@(posedge master_clk);
	cs <= 1'd1;
	
	#40;	
	
	
	$stop;
	
end

initial begin
    $monitor("Time=%0t | d_in=%b | cs=%b | d_out=%b | out_enable=%b | received_eval=%d | state=%d | final_evaluation=%d | is_max_or_min=%b | batch_size=%b | batch_first8=%b | batch_last8=%b | evaluation_stable=%b | evaluation_enable=%b | max_eval=%d | latched_evaluations=[%d, %d, %d, %d, %d, %d, %d] | receive_or_send_data=%b | final_evaluation_stable=%b | 2nd_board=%b",
		$time, d_in, uut.cs, d_out, uut.out_enable, received_evaluation, uut.state, uut.final_evaluation, uut.is_max_or_min, batch_size, uut.batch[87:80], uut.batch[7:0], uut.evaluation_stable, uut.evaluation_enable, uut.max_eval,
		uut.latched_evaluations[0], uut.latched_evaluations[1], uut.latched_evaluations[2], uut.latched_evaluations[3], uut.latched_evaluations[4], uut.latched_evaluations[5], uut.latched_evaluations[6], receive_or_send_data, final_evaluation_stable, uut.batch[(88 * 5) +: 84]);

end

endmodule