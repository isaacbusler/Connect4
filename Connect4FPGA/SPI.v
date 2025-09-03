/*
This module receives data from the raspberry pi, which includes
whether this is a max or min evaluation, the size of the batch being
sent, and the batch itself. It also sends the calculated evaluation
back to the raspberry pi.
*/

module SPI(
	input clk, // internal FPGA clock (e.g. 50MHz)
	input master_clk, // clk from rpi
	input cs, // cs line from pi
	input d_in, // serial data sent by pi
	input receive_or_send_data, // 1 to receive data, 0 to send
	input reset, // resets module
	input evaluation_stable, // 1 if evaluation is ready
	input signed [31:0] evaluation, // evaluation sent to pi
	output reg d_out, // serial out
	output reg out_enable, // 1 if fpga wants pi to raise cs
	output reg rpi_data_stable, // 1 if received data is stable
	output reg [7:0] is_max_or_min, // 1 = max, 0 = min
	output reg [7:0] batch_size, // num of boards
	output reg [615:0] batch, // max 7 boards
	output reg done_sending // 1 if done sending back eval
);

	
	// Synchronize master_clk to internal clk
	reg clk_sync_0, clk_sync_1;
	always @(posedge clk) begin
		clk_sync_0 <= master_clk;
		clk_sync_1 <= clk_sync_0;
	end

	// Detect rising edge of master_clk
	wire clk_rising = (clk_sync_1 && !clk_sync_0);
	wire clk_falling = (!clk_sync_1 && clk_sync_0);
	
	
	//state related
	(* preserve, noprune *) reg [2:0] state; //preserved for debugging
	localparam IDLE = 3'd0;
	localparam RECEIVE_DATA_BLOCK = 3'd1;
	localparam LATCH_VALUES = 3'd2;
	localparam SEND = 3'd3;
	localparam DONE = 3'd4;

	reg [9:0] count; // receive bit count
	reg [9:0] count_send; // send bit count
	reg [16:0] total_batch_bits; // expected batch bits
	reg [631:0] data_block; // 8 + 8 + 616 = 632 bits max
	
	localparam IS_MAX_OR_MIN_WIDTH = 8;
	localparam BATCH_SIZE_WIDTH    = 8;
	localparam MAX_BOARDS          = 7;
	localparam BOARD_WIDTH         = 88;
	localparam BATCH_WIDTH         = MAX_BOARDS * BOARD_WIDTH; // 616
	localparam DATA_BLOCK_WIDTH    = IS_MAX_OR_MIN_WIDTH + BATCH_SIZE_WIDTH + BATCH_WIDTH; // 632

	localparam IS_MAX_OR_MIN_MSB = DATA_BLOCK_WIDTH - 1;       // 631
	localparam IS_MAX_OR_MIN_LSB = IS_MAX_OR_MIN_MSB - IS_MAX_OR_MIN_WIDTH + 1; // 624
	localparam BATCH_SIZE_MSB    = IS_MAX_OR_MIN_LSB - 1;      // 623
	localparam BATCH_SIZE_LSB    = BATCH_SIZE_MSB - BATCH_SIZE_WIDTH + 1; // 616
	localparam BATCH_MSB         = BATCH_SIZE_LSB - 1;         // 615
	localparam BATCH_LSB         = 0;


	// FSM and receive logic on clk, gated by rising edge of master_clk
	always @(posedge clk) begin
		if (!reset) begin
			state <= IDLE;
			count <= 10'd0;
			is_max_or_min <= 8'd0;
			batch_size <= 8'd0;
			batch <= 616'd0;
			total_batch_bits <= 10'd0;
			out_enable <= 1'd0;
			rpi_data_stable <= 1'd0;
			done_sending <= 1'd0;
			data_block <= 0;
		end else if (clk_rising) begin
			// executes if cs is low, or in DONE state, or sending
			if (!cs || state == DONE || state == LATCH_VALUES || !receive_or_send_data) begin
				case (state)
					IDLE: begin
						rpi_data_stable <= 1'd0;
						done_sending <= 1'd0;
						out_enable <= 1'd0;
						count <= 10'd0;
						data_block <= 0;
						if (receive_or_send_data) state <= RECEIVE_DATA_BLOCK;
						else if (evaluation_stable) begin
							state <= SEND;
							out_enable <= 1;
						end
					end
					
					//receive data sent by the host
					RECEIVE_DATA_BLOCK: begin
						data_block <= {data_block[DATA_BLOCK_WIDTH-2:0], d_in};
						count <= count + 1;

						if (count == DATA_BLOCK_WIDTH - 1) begin
							state <= LATCH_VALUES;
						end
					end

					//correctly separate the data to the right values
					LATCH_VALUES: begin
						is_max_or_min <= data_block[IS_MAX_OR_MIN_MSB : IS_MAX_OR_MIN_LSB];
						batch_size    <= data_block[BATCH_SIZE_MSB : BATCH_SIZE_LSB];

						batch <= data_block[BATCH_MSB : BATCH_LSB];

						state <= DONE;
					end
					
					//send the evaluation back to the host
					SEND: begin
						out_enable <= 1;
						if (count_send == 10'd32) begin
							state <= DONE;
						end
					end

					//sets rpi_data_stable flag as 1 if receiving data
					//sets done_sending flag as 1 if sending data
					DONE: begin
						state <= IDLE;
						if (receive_or_send_data)
							rpi_data_stable <= 1'd1;
						else begin
							out_enable <= 0;
							done_sending <= 1;
						end
					end

					default: state <= IDLE;
				endcase
			end else begin
				state <= IDLE;
			end
		end
	end

	//sends out the evaluation to the FPGA on the neg edge
	always @(posedge clk or negedge reset) begin
		if (!reset) begin
			d_out <= 0;
			count_send <= 0;
		end else if (clk_falling && !cs) begin
			if (state == SEND && count_send < 10'd32) begin
				d_out <= evaluation[31 - count_send];
				count_send <= count_send + 1;
			end else begin
				d_out <= 0;
				count_send <= 0;
			end
		end
	end

endmodule