`timescale 1ns/1ps

module SPI_tb();

reg master_clk;
reg clk;
reg cs;
reg d_in;
reg receive_or_send_data;
reg reset;
reg [7:0] rpi_is_max_or_min;
reg [7:0] rpi_batch_size;
reg [615:0] rpi_batch;
reg [631:0] rpi_data;
wire d_out;
reg evaluation_stable;
reg [31:0] evaluation;
reg [31:0] received_evaluation;
wire out_enable;
wire rpi_data_stable;
wire [7:0] is_max_or_min;
wire [7:0] batch_size;
wire [615:0] batch;
wire done_sending;
integer i;
reg [4:0] ii;

initial begin
    clk = 0;
    forever #2 clk <= ~clk; // 125 MHz
end

initial begin
    master_clk = 0;
    forever #13 master_clk <= ~master_clk; // ~71 MHz — not a multiple of clk
end



SPI uut(
	.clk(clk),
	.master_clk(master_clk),
	.cs(cs),
	.d_in(d_in),
	.receive_or_send_data(receive_or_send_data),
	.reset(reset),
	.evaluation_stable(evaluation_stable),
	.evaluation(evaluation),
	.d_out(d_out),
	.out_enable(out_enable),
	.rpi_data_stable(rpi_data_stable),
	.is_max_or_min(is_max_or_min),
	.batch_size(batch_size),
	.batch(batch),
	.done_sending(done_sending)

);


initial begin
	reset = 0; //reset the spi instance
   #20 reset = 1; //release the reset
	#10 cs <= 1'd1; //cs is held high
	d_in <= 1'd0; //seraial data is initialized
	receive_or_send_data <= 1'd1; //receive_or_send_data set as receive
	rpi_is_max_or_min <= 8'd1; //is_max_or_min set as max
	rpi_batch_size <= 8'd1; //batch_size set as 1 board
	
	//batch being sent
	//rpi_batch <= {4'b1010, {608{1'b1}}, 4'b0101};
	rpi_batch <= {
    528'd0, // padding in upper bits [615:168]
		
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
    2'd0, 2'd0, 2'd0, 2'd0, 2'd2, 2'd0, 2'd1,
	 4'd0 //padding
};

	//combine all the data together
	#10 rpi_data <= {rpi_is_max_or_min, rpi_batch_size, rpi_batch};
	
	//cs is brought low to send data
	#10 cs <= 1'd0;
	
	//@(posedge master_clk);
	//@(posedge master_clk);
	
	//data is received
	@(posedge master_clk);
	for (i = 631; i >= 0; i = i - 1) begin
		@(negedge master_clk);
		d_in <= rpi_data[i];
	end
	
	//cs is brought back high after sending data
	@(posedge master_clk);
	cs <= 1'd1;
	
	
	
	
	
	evaluation <= {4'b1010, {24{1'b1}}, 4'b0101}; //evaluation to be sent back
	received_evaluation <= 0; //received_evaluation initialized to 0
	ii <= 0;
	#1000;
	reset = 0; //reset module again
   #20 reset = 1; //release reset
	receive_or_send_data <= 0; //receive_or_send_data set as send
	evaluation_stable <= 1; //evaluation is now set to be stable
	
	//waits for out_enable to bring cs low to send data
	wait(out_enable == 1);
	cs <= 0;
	
	@(posedge master_clk);

	//evaluation is sent
	for (i = 0; i < 32; i = i + 1) begin
		 @(posedge master_clk);
		 received_evaluation <= {received_evaluation[30:0], d_out};
		 ii <= ii + 1;
	end
	
	//evaluation is now set to not be stable
	@(posedge master_clk);
	evaluation_stable <= 0;
	
	//waits for out_enable to be 0 to bring cs low after receiving data
	wait(out_enable == 0);
	@(posedge master_clk);
	cs <= 1'd1;
	receive_or_send_data <= 1;
		
	#20;
	$stop;
	
end


initial begin
    $monitor("Time=%0t | receive_or_send_data=%b | d_in=%b | cs=%b | count=%d | d_out=%b | ii=%d | out_enable=%b | received_eval=%b | rpi_data_stable=%b | total_batch_bits=%d | is_max_or_min=%b | batch_size=%b | state=%d | batch_first8=%b | batch_last8=%b | data_block=%b",
         $time, receive_or_send_data, d_in, uut.cs, uut.count, d_out, ii, uut.out_enable, received_evaluation, uut.rpi_data_stable, uut.temp_total_bits, is_max_or_min, batch_size, uut.state,
         batch[87:80], batch[7:0], uut.data_block); //615:608
end


endmodule
